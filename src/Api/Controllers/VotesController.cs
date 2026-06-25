using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    [HttpPost("{tweetId:guid}")]
    public IActionResult CastVote(Guid tweetId)
    {
        return Ok();
    }
}
