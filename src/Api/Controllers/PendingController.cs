using Api.Extensions;
using Api.Mappers;
using Api.Models.Responses;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/pending")]
[Authorize(Roles = "Contributor,Admin")]
public class PendingController : ControllerBase
{
    private readonly IPendingService _pendingService;
    private readonly TweetDtoMapper _tweetDtoMapper;

    public PendingController(IPendingService pendingService, TweetDtoMapper tweetDtoMapper)
    {
        _pendingService = pendingService;
        _tweetDtoMapper = tweetDtoMapper;
    }

    [HttpGet("additions")]
    [ProducesResponseType(typeof(List<PendingSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingAdditions(CancellationToken ct)
    {
        var result = await _pendingService.GetPendingAdditionsAsync(ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(ps => new PendingSubmissionDto(
            _tweetDtoMapper.ToDto(ps.Tweet),
            ps.RequestedFolders.Select(f => new PendingFolderDto(f.FolderId, f.FolderName, f.SubmittedAt)).ToList()))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("additions/{folderId:guid}/{tweetId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApprovePendingAddition(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _pendingService.ApprovePendingAdditionAsync(folderId, tweetId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("additions/{folderId:guid}/{tweetId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectPendingAddition(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _pendingService.RejectPendingAdditionAsync(folderId, tweetId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
