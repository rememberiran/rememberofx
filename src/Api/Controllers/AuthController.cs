using Api.Extensions;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify([FromQuery] string xUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xUserId))
            return BadRequest(new { message = "xUserId query parameter is required" });

        var result = await _authService.VerifyAndGenerateTokenAsync(xUserId, ct);

        return result.IsSuccess
            ? Ok(new { token = result.Value })
            : result.ToActionResult();
    }
}
