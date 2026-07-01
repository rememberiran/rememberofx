using Api.Extensions;
using Api.Models.Responses;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/removal-requests")]
public class RemovalRequestsController : ControllerBase
{
    private readonly IRemovalRequestService _removalRequestService;

    public RemovalRequestsController(IRemovalRequestService removalRequestService)
    {
        _removalRequestService = removalRequestService;
    }

    [HttpPost("/api/folders/{folderId:guid}/tweets/{tweetId:guid}/removal-request")]
    [Authorize]
    [ProducesResponseType(typeof(RemovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var result = await _removalRequestService.SubmitAsync(folderId, tweetId, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(List<RemovalRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await _removalRequestService.GetPendingAsync(ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value!.Select(ToDto).ToList());
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(RemovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _removalRequestService.ApproveAsync(id, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(RemovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await _removalRequestService.RejectAsync(id, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(ToDto(result.Value!));
    }

    private static RemovalRequestDto ToDto(Domain.Entities.RemovalRequest r)
    {
        return new RemovalRequestDto(
            r.Id,
            r.FolderId,
            r.FolderName,
            r.TweetId,
            r.TweetXId,
            r.RequestedByUserId,
            r.RequestedAt,
            r.Status,
            r.ResolvedAt,
            r.Approvals.Select(a => new RemovalApprovalDto(
                a.Id,
                a.ApprovedByUserId,
                a.ApprovedByXUsername,
                a.ApprovedAt,
                a.IsVoid)).ToList());
    }
}
