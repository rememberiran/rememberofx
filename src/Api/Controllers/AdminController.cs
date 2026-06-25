using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    [HttpGet("users")]
    public IActionResult ListUsers()
    {
        return Ok(Array.Empty<object>());
    }

    [HttpPost("users")]
    public IActionResult AddUser()
    {
        return Created();
    }

    [HttpPut("users/{id:guid}")]
    public IActionResult UpdateUser(Guid id)
    {
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public IActionResult DeactivateUser(Guid id)
    {
        return NoContent();
    }
}
