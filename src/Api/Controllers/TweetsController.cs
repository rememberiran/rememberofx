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
    public async Task<IActionResult> Submit([FromBody] SubmitTweetRequest request, CancellationToken ct)
    {
        var command = new SubmitTweetCommand(
            TweetUrl: request.TweetUrl,
            FolderIds: request.FolderIds,
            SubmittedByUserId: null);

        var result = await _submissionService.SubmitAsync(command, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Accepted(new SubmitTweetResponse(result.Value!.Id, result.Value.FetchStatus.ToString()));
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
}
