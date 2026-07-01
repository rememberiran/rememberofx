using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class ViolationReportMapper
{
    public static ViolationReport ToDomain(ViolationReportRecord record)
    {
        return new ViolationReport
        {
            Id = record.Id,
            ReportedUserId = record.ReportedUserId,
            ReportedXUsername = record.ReportedUser?.XUsername ?? string.Empty,
            ReportedByUserId = record.ReportedByUserId,
            ReportedByIp = record.ReportedByIp,
            Explanation = record.Explanation,
            CreatedAt = record.CreatedAt,
            Status = record.Status,
            ReviewedAt = record.ReviewedAt,
            ReviewedByUserId = record.ReviewedByUserId,
        };
    }
}
