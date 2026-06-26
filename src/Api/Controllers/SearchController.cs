using Api.Extensions;
using Api.Mappers;
using Api.Models.Responses;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public SearchController(ISearchService searchService, TweetDtoMapper tweetDtoMapper)
    {
        _searchService = searchService;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SearchTweetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
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
        var result = await _searchService.SearchAsync(query, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var data = result.Value!;
        var items = _tweetDtoMapper.ToDtoList(data.Items);
        var subjectProfile = data.SubjectProfile != null
            ? XUserProfileDtoMapper.ToDto(data.SubjectProfile)
            : null;

        return Ok(new SearchTweetsResponse(items, data.TotalCount, subjectProfile));
    }
}
