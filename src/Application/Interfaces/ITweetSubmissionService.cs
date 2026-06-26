using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface ITweetSubmissionService
{
    Task<Result<Tweet>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct);
}
