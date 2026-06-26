using Api.Extensions;
using Api.Models.Requests;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TweetsController : ControllerBase
{
    private readonly ITweetSubmissionService _submissionService;
    private readonly ITweetQueryService _queryService;

    public TweetsController(ITweetSubmissionService submissionService, ITweetQueryService queryService)
    {
        _submissionService = submissionService;
        _queryService = queryService;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitTweetRequest request, CancellationToken ct)
    {
        var command = new SubmitTweetCommand(
            TweetUrl: request.TweetUrl,
            FolderIds: request.FolderIds,
            SubmittedByIp: HttpContext.GetClientIp(),
            SubmittedByUserId: null);

        var result = await _submissionService.SubmitAsync(command, ct);

        return result.IsSuccess
            ? Accepted(new { tweetId = result.Value!.TweetId, fetchStatus = result.Value.FetchStatus })
            : result.ToActionResult();
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetStatusAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? tag,
        [FromQuery] string? username,
        [FromQuery] string? userId,
        [FromQuery] string sort = "votes",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new SearchTweetsQuery(q, tag, username, userId, sort, page, pageSize);
        var result = await _queryService.SearchAsync(query, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var data = result.Value!;
        return Ok(new { items = data.Items, totalCount = data.TotalCount, subjectProfile = data.SubjectProfile });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);
        return result.ToActionResult();
    }
}
