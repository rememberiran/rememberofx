using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface IViolationReportService
{
    Task<Result<ViolationReport>> SubmitAsync(Guid reportedUserId, string explanation, CancellationToken ct);
    Task<Result<List<ViolationReport>>> GetAllAsync(CancellationToken ct);
    Task<Result<ViolationReport>> DismissAsync(Guid reportId, CancellationToken ct);
}
