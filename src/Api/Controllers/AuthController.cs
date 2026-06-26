using Api.Extensions;
using Api.Models.Requests;
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

    [HttpPost("token")]
    public async Task<IActionResult> ExchangeToken([FromBody] ExchangeTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.ExchangeTokenAsync(
            request.XAccessToken,
            HttpContext.GetClientIp(),
            ct);

        return result.IsSuccess
            ? Ok(new { token = result.Value!.Token, expiresAt = result.Value.ExpiresAt })
            : result.ToActionResult();
    }
}
