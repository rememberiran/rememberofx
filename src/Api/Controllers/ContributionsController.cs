using Api.Models.Responses;
using Application;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/contributions")]
public class ContributionsController : ControllerBase
{
    private readonly IAppDbContext _db;

    private static readonly HashSet<string> VisibleActions = new(StringComparer.Ordinal)
    {
        "Folder.Created",
        "Tweet.AddedToFolder",
        "Tweet.RemovedFromFolder",
        "Submission.Approved",
        "Submission.Rejected",
    };

    public ContributionsController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ContributionsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContributions(
        [FromQuery] Guid? contributorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var actions = VisibleActions.ToList();

        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(a => a.PerformedByUser)
            .Where(a => actions.Contains(a.Action));

        if (contributorId.HasValue)
        {
            query = query.Where(a => a.PerformedByUserId == contributorId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(a => new ContributionEntryDto(
            a.PerformedByUserId,
            a.PerformedByUser?.XUsername,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.CreatedAt)).ToList();

        return Ok(new ContributionsResponse(dtos, totalCount));
    }
}
