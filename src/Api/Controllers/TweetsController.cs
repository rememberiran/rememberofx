using System.Globalization;
using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Application.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TweetsController : ControllerBase
{
    private readonly ITweetSubmissionService _submissionService;
    private readonly ITweetQueryService _queryService;
    private readonly IVoteService _voteService;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public TweetsController(
        ITweetSubmissionService submissionService,
        ITweetQueryService queryService,
        IVoteService voteService,
        IAsyncContext<IdentityContext> identityContext,
        TweetDtoMapper tweetDtoMapper)
    {
        _submissionService = submissionService;
        _queryService = queryService;
        _voteService = voteService;
        _identityContext = identityContext;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmitTweetResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit([FromBody] SubmitTweetRequest request, CancellationToken ct)
    {
        var command = new SubmitTweetCommand(
            TweetUrl: request.TweetUrl,
            FolderIds: request.FolderIds,
            SubmittedByUserId: _identityContext.Value?.InternalUserId,
            IsAnonymous: request.IsAnonymous);

        var result = await _submissionService.SubmitAsync(command, ct);

        if (!result.IsSuccess)
        {
            if (result.Error!.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                var quotaResult = await _submissionService.GetQuotaAsync(ct);
                if (quotaResult.IsSuccess)
                {
                    SetRateLimitHeaders(quotaResult.Value!);
                }
            }

            return result.ToActionResult();
        }

        var data = result.Value!;
        SetRateLimitHeaders(data.Quota);

        var quotaDto = ToQuotaDto(data.Quota);
        return Accepted(new SubmitTweetResponse(data.Tweet.Id, data.Tweet.FetchStatus.ToString(), quotaDto));
    }

    [HttpGet("quota")]
    [ProducesResponseType(typeof(SubmissionQuotaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetQuota(CancellationToken ct)
    {
        var result = await _submissionService.GetQuotaAsync(ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var q = result.Value!;
        SetRateLimitHeaders(q);

        return Ok(ToQuotaDto(q));
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(TweetStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetStatusAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var tweet = result.Value!;
        object? tweetData = tweet.FetchStatus switch
        {
            FetchStatus.Ok => _tweetDtoMapper.ToDto(tweet),
            FetchStatus.NotFound or FetchStatus.Private or FetchStatus.ScrapeFailed => new
            {
                fetchStatus = tweet.FetchStatus.ToString(),
                xTweetUrl = tweet.XTweetUrl,
            },
            _ => null,
        };

        return Ok(new TweetStatusResponse(tweet.Id, tweet.FetchStatus.ToString(), tweetData));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var tweet = result.Value!;
        var userId = _identityContext.Value?.InternalUserId;
        HashSet<Guid>? votedIds = null;
        if (userId.HasValue)
        {
            var votedResult = await _voteService.GetVotedTweetIdsAsync(userId.Value, new[] { tweet.Tweet.Id }, ct);
            if (votedResult.IsSuccess)
            {
                votedIds = votedResult.Value;
            }
        }

        var dto = _tweetDtoMapper.ToDto(tweet, votedIds);
        return Ok(dto);
    }

    private static SubmissionQuotaDto ToQuotaDto(SubmissionQuota q)
    {
        return new SubmissionQuotaDto(
            q.HourlyRemaining,
            q.HourlyLimit,
            q.HourlyResetAt,
            q.DailyRemaining,
            q.DailyLimit,
            q.DailyResetAt);
    }

    private void SetRateLimitHeaders(SubmissionQuota quota)
    {
        var headers = Response.Headers;
        headers[$"X-RateLimit-Remaining-Hour"] = quota.HourlyRemaining.ToString(CultureInfo.InvariantCulture);
        headers[$"X-RateLimit-Remaining-Day"] = quota.DailyRemaining.ToString(CultureInfo.InvariantCulture);

        var earlierReset = quota.HourlyRemaining <= 0 ? quota.HourlyResetAt
            : quota.DailyRemaining <= 0 ? quota.DailyResetAt
            : (quota.HourlyResetAt < quota.DailyResetAt ? quota.HourlyResetAt : quota.DailyResetAt);

        var retryAfterSeconds = Math.Max(0, (int)Math.Ceiling((earlierReset - DateTime.UtcNow).TotalSeconds));
        headers[$"X-RateLimit-Reset"] = earlierReset.ToString("o", CultureInfo.InvariantCulture);
        headers[$"Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
    }
}
