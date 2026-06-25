using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/xusers")]
public class XUserProfilesController : ControllerBase
{
    [HttpGet("{xUserId}")]
    public IActionResult GetProfile(string xUserId)
    {
        return NotFound();
    }

    [HttpPut("{xUserId}")]
    public IActionResult UpsertProfile(string xUserId)
    {
        return NoContent();
    }
}
