using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpGet("verify")]
    public IActionResult Verify([FromQuery] string xUserId)
    {
        return Unauthorized();
    }
}
