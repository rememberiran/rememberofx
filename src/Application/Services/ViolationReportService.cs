using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class ViolationReportService : IViolationReportService
{
    private readonly IAppDbContext _db;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<ViolationReportService> _logger;

    private static readonly EventId ViolationReportSubmittedEvent = new(1070, "ViolationReportSubmitted");
    private static readonly EventId ViolationReportDismissedEvent = new(1071, "ViolationReportDismissed");

    public ViolationReportService(
        IAppDbContext db,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<ViolationReportService> logger)
    {
        _db = db;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result<ViolationReport>> SubmitAsync(Guid reportedUserId, string explanation, CancellationToken ct)
    {
        var identity = _identityContext.Value!;

        var targetExists = await _db.Users.AnyAsync(u => u.Id == reportedUserId && u.IsActive, ct);
        if (!targetExists)
        {
            return Result.Failure<ViolationReport>(DomainError.NotFound("User not found"));
        }

        var record = new ViolationReportRecord
        {
            Id = Guid.NewGuid(),
            ReportedUserId = reportedUserId,
            ReportedByUserId = identity.InternalUserId,
            ReportedByIp = identity.IpAddress,
            Explanation = explanation,
            Status = "pending",
        };

        _db.ViolationReports.Add(record);

        _logger.LogInformation(
            ViolationReportSubmittedEvent,
            "Violation report submitted: {ReportId} against user {ReportedUserId}",
            record.Id,
            reportedUserId);

        return Result.Success(new ViolationReport
        {
            Id = record.Id,
            ReportedUserId = reportedUserId,
            ReportedXUsername = string.Empty,
            ReportedByUserId = record.ReportedByUserId,
            ReportedByIp = record.ReportedByIp,
            Explanation = explanation,
            CreatedAt = record.CreatedAt,
            Status = record.Status,
        });
    }

    public async Task<Result<List<ViolationReport>>> GetAllAsync(CancellationToken ct)
    {
        var records = await _db.ViolationReports
            .AsNoTracking()
            .Include(v => v.ReportedUser)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

        return Result.Success(records.Select(ViolationReportMapper.ToDomain).ToList());
    }

    public async Task<Result<ViolationReport>> DismissAsync(Guid reportId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<ViolationReport>(DomainError.Unauthorized("User not authenticated"));
        }

        var report = await _db.ViolationReports
            .Include(v => v.ReportedUser)
            .FirstOrDefaultAsync(v => v.Id == reportId, ct);

        if (report is null)
        {
            return Result.Failure<ViolationReport>(DomainError.NotFound("Report not found"));
        }

        if (!string.Equals(report.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure<ViolationReport>(DomainError.Validation("Report is not pending"));
        }

        report.Status = "dismissed";
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewedByUserId = userId;

        _logger.LogInformation(
            ViolationReportDismissedEvent,
            "Violation report {ReportId} dismissed by {UserId}",
            reportId,
            userId);

        return Result.Success(ViolationReportMapper.ToDomain(report));
    }
}
