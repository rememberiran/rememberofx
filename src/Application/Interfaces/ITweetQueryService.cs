using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface ITweetQueryService
{
    Task<Result<TweetWithAuthor>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<Tweet>> GetStatusAsync(Guid id, CancellationToken ct);
}
