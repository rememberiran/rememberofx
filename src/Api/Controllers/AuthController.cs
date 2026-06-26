using Api.Extensions;
using Api.Models.Requests;
using Api.Models.Responses;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExchangeToken([FromBody] ExchangeTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.ExchangeTokenAsync(request.XAccessToken, ct);

        return result.IsSuccess
            ? Ok(new AuthTokenResponse(result.Value!.Token, result.Value.ExpiresAt))
            : result.ToActionResult();
    }

    [HttpPost("dev-token")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDevToken([FromBody] DevTokenRequest request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _authService.GenerateDevTokenAsync(request.XUserId, ct);

        return result.IsSuccess
            ? Ok(new AuthTokenResponse(result.Value!.Token, result.Value.ExpiresAt))
            : result.ToActionResult();
    }
}
