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
[Route("api/xusers")]
public class XUserProfilesController : ControllerBase
{
    private readonly IXUserProfileService _profileService;
    private readonly IAsyncContext<IdentityContext> _identityContext;

    public XUserProfilesController(IXUserProfileService profileService, IAsyncContext<IdentityContext> identityContext)
    {
        _profileService = profileService;
        _identityContext = identityContext;
    }

    [HttpGet("{xUserId}")]
    [ProducesResponseType(typeof(XUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string xUserId, CancellationToken ct)
    {
        var result = await _profileService.GetByXUserIdAsync(xUserId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var statsResult = await _profileService.GetAuthorStatsAsync(result.Value!.XUserId, ct);

        if (!statsResult.IsSuccess)
        {
            return statsResult.ToActionResult();
        }

        var stats = statsResult.Value!;
        var dto = XUserProfileDtoMapper.ToDto(
            result.Value!,
            stats.ArchivedTweetCount,
            stats.TotalVotesReceived,
            stats.FirstArchivedAt);
        return Ok(dto);
    }

    [HttpPut("{xUserId}")]
    [Authorize(Roles = "Contributor,Admin")]
    [ProducesResponseType(typeof(XUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertProfile(string xUserId, [FromBody] UpsertXUserProfileRequest request, CancellationToken ct)
    {
        var result = await _profileService.UpsertAsync(xUserId, request.CustomName, request.Description, _identityContext.Value?.InternalUserId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = XUserProfileDtoMapper.ToDto(result.Value!);
        return Ok(dto);
    }
}
