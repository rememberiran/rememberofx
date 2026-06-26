using Api.Extensions;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _voteService;
    private readonly IAsyncContext<IdentityContext> _identityContext;

    public VotesController(IVoteService voteService, IAsyncContext<IdentityContext> identityContext)
    {
        _voteService = voteService;
        _identityContext = identityContext;
    }

    [HttpPost("{tweetId:guid}")]
    public async Task<IActionResult> CastVote(Guid tweetId, CancellationToken ct)
    {
        var result = await _voteService.CastVoteAsync(tweetId, _identityContext.Value?.InternalUserId, ct);

        return result.IsSuccess
            ? Ok(new { message = $"Vote recorded" })
            : result.ToActionResult();
    }
}
