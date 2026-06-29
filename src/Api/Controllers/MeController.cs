using Api.Extensions;
using Api.Mappers;
using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Roles = "Contributor,Admin")]
public class MeController : ControllerBase
{
    private readonly ITweetQueryService _queryService;
    private readonly ISearchService _searchService;
    private readonly IVoteService _voteService;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public MeController(
        ITweetQueryService queryService,
        ISearchService searchService,
        IVoteService voteService,
        IAsyncContext<IdentityContext> identityContext,
        TweetDtoMapper tweetDtoMapper)
    {
        _queryService = queryService;
        _searchService = searchService;
        _voteService = voteService;
        _identityContext = identityContext;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(SubmitterStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _queryService.GetSubmitterStatsAsync(userId.Value, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpGet("tweets")]
    [ProducesResponseType(typeof(SearchTweetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTweets(
        [FromQuery] string sort = "date",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var query = new SearchTweetsQuery(
            Q: null,
            Tag: null,
            Username: null,
            UserId: null,
            SubmittedByUserId: userId.Value,
            Sort: sort,
            Page: page,
            PageSize: pageSize);

        var result = await _searchService.SearchAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var data = result.Value!;

        HashSet<Guid>? votedIds = null;
        var tweetIds = data.Items.Select(i => i.Tweet.Id).ToList();
        var votedResult = await _voteService.GetVotedTweetIdsAsync(userId.Value, tweetIds, ct);
        if (votedResult.IsSuccess)
        {
            votedIds = votedResult.Value;
        }

        var items = _tweetDtoMapper.ToDtoList(data.Items, votedIds);
        return Ok(new SearchTweetsResponse(items, data.TotalCount, null));
    }
}
