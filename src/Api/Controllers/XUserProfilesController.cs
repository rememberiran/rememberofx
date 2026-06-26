using Api.Extensions;
using Api.Models.Requests;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/xusers")]
public class XUserProfilesController : ControllerBase
{
    private readonly IXUserProfileService _profileService;
    private readonly IUserService _userService;

    public XUserProfilesController(IXUserProfileService profileService, IUserService userService)
    {
        _profileService = profileService;
        _userService = userService;
    }

    [HttpGet("{xUserId}")]
    public async Task<IActionResult> GetProfile(string xUserId, CancellationToken ct)
    {
        var result = await _profileService.GetByXUserIdAsync(xUserId, ct);
        return result.ToActionResult();
    }

    [HttpPut("{xUserId}")]
    public async Task<IActionResult> UpsertProfile(string xUserId, [FromBody] UpsertXUserProfileRequest request, CancellationToken ct)
    {
        Guid? userId = null;
        var xUser = User.GetXUserId();
        if (xUser != null)
        {
            var userResult = await _userService.GetByXUserIdAsync(xUser, ct);
            if (userResult.IsSuccess)
                userId = userResult.Value!.Id;
        }

        var result = await _profileService.UpsertAsync(xUserId, request.CustomName, request.Description, userId, ct);
        return result.ToActionResult();
    }
}
