using Api.Extensions;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _voteService;
    private readonly IUserService _userService;

    public VotesController(IVoteService voteService, IUserService userService)
    {
        _voteService = voteService;
        _userService = userService;
    }

    [HttpPost("{tweetId:guid}")]
    public async Task<IActionResult> CastVote(Guid tweetId, CancellationToken ct)
    {
        Guid? voterUserId = null;
        var xUserId = User.GetXUserId();
        if (xUserId != null)
        {
            var userResult = await _userService.GetByXUserIdAsync(xUserId, ct);
            if (userResult.IsSuccess)
                voterUserId = userResult.Value!.Id;
        }

        var result = await _voteService.CastVoteAsync(tweetId, HttpContext.GetClientIp(), voterUserId, ct);

        return result.IsSuccess
            ? Ok(new { message = "Vote recorded" })
            : result.ToActionResult();
    }
}
