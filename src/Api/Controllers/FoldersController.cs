using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;
    private readonly IVoteService _voteService;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public FoldersController(
        IFolderService folderService,
        IVoteService voteService,
        IAsyncContext<IdentityContext> identityContext,
        TweetDtoMapper tweetDtoMapper)
    {
        _folderService = folderService;
        _voteService = voteService;
        _identityContext = identityContext;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FolderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _folderService.ListRootFoldersAsync(ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(FolderDtoMapper.ToSummaryDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(List<FolderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _folderService.ListByCreatorAsync(userId.Value, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(FolderDtoMapper.ToSummaryDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _folderService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var folder = result.Value!;
        var childrenResult = await _folderService.GetChildrenAsync(id, ct);
        var children = childrenResult.IsSuccess
            ? childrenResult.Value!.Select(FolderDtoMapper.ToSummaryDto).ToList()
            : new List<FolderSummaryDto>();
        var activeChildren = folder.Children.Where(c => c.IsActive).ToList();

        var breadcrumb = new List<FolderBreadcrumbDto>();
        var current = folder;
        while (current.ParentFolder != null)
        {
            breadcrumb.Insert(0, FolderDtoMapper.ToBreadcrumbDto(current.ParentFolder));
            current = current.ParentFolder;
        }

        var dto = FolderDtoMapper.ToDto(folder, activeChildren.Count, children, breadcrumb);
        return Ok(dto);
    }

    [HttpGet("{id:guid}/children")]
    [ProducesResponseType(typeof(List<FolderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct)
    {
        var result = await _folderService.GetChildrenAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(FolderDtoMapper.ToSummaryDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}/tweets")]
    [ProducesResponseType(typeof(FolderTweetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTweets(
        Guid id,
        [FromQuery] string sort = "votes",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _folderService.GetTweetsAsync(id, sort, page, pageSize, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var data = result.Value!;

        var userId = _identityContext.Value?.InternalUserId;
        HashSet<Guid>? votedIds = null;
        if (userId.HasValue)
        {
            var tweetIds = data.Items.Select(i => i.Tweet.Id).ToList();
            var votedResult = await _voteService.GetVotedTweetIdsAsync(userId.Value, tweetIds, ct);
            if (votedResult.IsSuccess)
            {
                votedIds = votedResult.Value;
            }
        }

        var items = _tweetDtoMapper.ToDtoList(data.Items, votedIds);
        return Ok(new FolderTweetsResponse(items, data.TotalCount));
    }

    [HttpPost]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _folderService.CreateAsync(
            request.Name, request.Description, request.Icon, request.ParentFolderId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = FolderDtoMapper.ToDto(result.Value!, 0);
        return Created(new Uri($"/api/folders/{result.Value!.Id}", UriKind.Relative), dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request, CancellationToken ct)
    {
        var result = await _folderService.UpdateAsync(id, request.Name, request.Description, request.Icon, request.ParentFolderId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = FolderDtoMapper.ToDto(result.Value!, 0);
        return Ok(dto);
    }

    [HttpPost("{folderId:guid}/tweets/{tweetId:guid}")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTweet(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _folderService.AddTweetAsync(folderId, tweetId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("{folderId:guid}/tweets/{tweetId:guid}")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTweet(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _folderService.RemoveTweetAsync(folderId, tweetId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
