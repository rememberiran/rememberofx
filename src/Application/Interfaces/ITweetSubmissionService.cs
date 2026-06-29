using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface ITweetSubmissionService
{
    Task<Result<SubmissionResultWithQuota>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct);
    Task<Result<SubmissionQuota>> GetQuotaAsync(CancellationToken ct);
}
