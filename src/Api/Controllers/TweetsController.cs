using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
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
    private readonly TweetDtoMapper _tweetDtoMapper;

    public TweetsController(
        ITweetSubmissionService submissionService,
        ITweetQueryService queryService,
        TweetDtoMapper tweetDtoMapper)
    {
        _submissionService = submissionService;
        _queryService = queryService;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpPost]
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

        return Accepted(new
        {
            tweetId = result.Value!.Id,
            fetchStatus = result.Value.FetchStatus.ToString(),
        });
    }

    [HttpGet("{id:guid}/status")]
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

        return Ok(new
        {
            tweetId = tweet.Id,
            fetchStatus = tweet.FetchStatus.ToString(),
            tweetData,
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = _tweetDtoMapper.ToDto(result.Value!);
        return Ok(dto);
    }
}
