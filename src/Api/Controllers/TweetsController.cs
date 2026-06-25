using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TweetsController : ControllerBase
{
    [HttpPost]
    public IActionResult Submit()
    {
        return Accepted();
    }

    [HttpGet("{id:guid}/status")]
    public IActionResult GetStatus(Guid id)
    {
        return Ok(new { tweetId = id, fetchStatus = "Pending" });
    }

    [HttpGet("search")]
    public IActionResult Search()
    {
        return Ok(new { items = Array.Empty<object>(), totalCount = 0 });
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return NotFound();
    }
}
