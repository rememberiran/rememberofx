using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        return Ok(Array.Empty<object>());
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return NotFound();
    }

    [HttpGet("{id:guid}/children")]
    public IActionResult GetChildren(Guid id)
    {
        return Ok(Array.Empty<object>());
    }

    [HttpGet("{id:guid}/tweets")]
    public IActionResult GetTweets(Guid id)
    {
        return Ok(Array.Empty<object>());
    }

    [HttpPost]
    public IActionResult Create()
    {
        return Created();
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id)
    {
        return NoContent();
    }

    [HttpPost("{folderId:guid}/tweets/{tweetId:guid}")]
    public IActionResult AddTweet(Guid folderId, Guid tweetId)
    {
        return NoContent();
    }

    [HttpDelete("{folderId:guid}/tweets/{tweetId:guid}")]
    public IActionResult RemoveTweet(Guid folderId, Guid tweetId)
    {
        return NoContent();
    }
}
