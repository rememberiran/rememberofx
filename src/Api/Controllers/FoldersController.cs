using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;
    private readonly IUserService _userService;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public FoldersController(
        IFolderService folderService,
        IUserService userService,
        TweetDtoMapper tweetDtoMapper)
    {
        _folderService = folderService;
        _userService = userService;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpGet]
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _folderService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var folder = result.Value!;
        var activeChildren = folder.Children.Where(c => c.IsActive).ToList();
        var children = activeChildren.Select(c => FolderDtoMapper.ToSummaryDto(c, 0)).ToList();

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
        var items = _tweetDtoMapper.ToDtoList(data.Items);
        return Ok(new { items, totalCount = data.TotalCount });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(ct);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _folderService.CreateAsync(
            request.Name, request.Description, request.ParentFolderId, userId.Value, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = FolderDtoMapper.ToDto(result.Value!, 0);
        return Created(new Uri($"/api/folders/{result.Value!.Id}", UriKind.Relative), dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request, CancellationToken ct)
    {
        var result = await _folderService.UpdateAsync(id, request.Name, request.Description, request.ParentFolderId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = FolderDtoMapper.ToDto(result.Value!, 0);
        return Ok(dto);
    }

    [HttpPost("{folderId:guid}/tweets/{tweetId:guid}")]
    public async Task<IActionResult> AddTweet(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(ct);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _folderService.AddTweetAsync(folderId, tweetId, userId.Value, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("{folderId:guid}/tweets/{tweetId:guid}")]
    public async Task<IActionResult> RemoveTweet(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _folderService.RemoveTweetAsync(folderId, tweetId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    private async Task<Guid?> ResolveUserIdAsync(CancellationToken ct)
    {
        var xUserId = User.GetXUserId();
        if (xUserId is null)
        {
            return null;
        }

        var userResult = await _userService.GetByXUserIdAsync(xUserId, ct);
        return userResult.IsSuccess ? userResult.Value!.Id : null;
    }
}
