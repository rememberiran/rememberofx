using Api.Extensions;
using Api.Models.Requests;
using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/contributors")]
public class ContributorsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IViolationReportService _violationReportService;
    private readonly IAppDbContext _db;
    private readonly IAsyncContext<IdentityContext> _identityContext;

    public ContributorsController(
        IUserService userService,
        IViolationReportService violationReportService,
        IAppDbContext db,
        IAsyncContext<IdentityContext> identityContext)
    {
        _userService = userService;
        _violationReportService = violationReportService;
        _db = db;
        _identityContext = identityContext;
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ContributorProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(userId, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var user = result.Value!;

        var folderCount = await _db.Folders.CountAsync(f => f.CreatedByUserId == userId && f.IsActive, ct);
        var tweetsAdded = await _db.FolderTweets.CountAsync(ft => ft.AddedByUserId == userId && ft.Status == "approved", ct);

        var dto = new ContributorProfileDto(
            user.Id,
            user.XUserId,
            user.XUsername,
            user.Role,
            IsSuspended: user.SuspendedAt.HasValue,
            SuspendedReason: user.SuspendedAt.HasValue ? user.SuspendedReason : null,
            FolderCount: folderCount,
            TweetsAdded: tweetsAdded);

        return Ok(dto);
    }

    [HttpPost("{userId:guid}/report")]
    [ProducesResponseType(typeof(ViolationReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(Guid userId, [FromBody] ReportContributorRequest request, CancellationToken ct)
    {
        var result = await _violationReportService.SubmitAsync(userId, request.Explanation, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var v = result.Value!;
        return Ok(new ViolationReportDto(
            v.Id,
            v.ReportedUserId,
            v.ReportedXUsername,
            v.ReportedByUserId,
            v.Explanation,
            v.CreatedAt,
            v.Status,
            v.ReviewedAt));
    }
}
