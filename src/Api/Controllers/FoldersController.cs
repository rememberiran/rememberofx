using Api.Extensions;
using Api.Models.Requests;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;
    private readonly IUserService _userService;

    public FoldersController(IFolderService folderService, IUserService userService)
    {
        _folderService = folderService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _folderService.ListRootFoldersAsync(ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _folderService.GetByIdAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/children")]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct)
    {
        var result = await _folderService.GetChildrenAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/tweets")]
    public async Task<IActionResult> GetTweets(
        Guid id,
        [FromQuery] string sort = $"votes",
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
        return Ok(new { items = data.Items, totalCount = data.TotalCount });
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

        return result.IsSuccess
            ? Created(new Uri($"/api/folders/{result.Value!.Id}", UriKind.Relative), result.Value)
            : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request, CancellationToken ct)
    {
        var result = await _folderService.UpdateAsync(id, request.Name, request.Description, request.ParentFolderId, ct);
        return result.ToActionResult();
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
