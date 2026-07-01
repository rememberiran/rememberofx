using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IViolationReportService _violationReportService;
    private readonly IAsyncContext<IdentityContext> _identityContext;

    public AdminController(
        IUserService userService,
        IViolationReportService violationReportService,
        IAsyncContext<IdentityContext> identityContext)
    {
        _userService = userService;
        _violationReportService = violationReportService;
        _identityContext = identityContext;
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var result = await _userService.ListAllAsync(ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(UserDtoMapper.ToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost("users")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser([FromBody] AddUserRequest request, CancellationToken ct)
    {
        var result = await _userService.AddAsync(request.XUserId, request.Role, _identityContext.Value?.InternalUserId, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = UserDtoMapper.ToDto(result.Value!);
        return Created(new Uri($"/api/admin/users/{result.Value!.Id}", UriKind.Relative), dto);
    }

    [HttpPut("users/{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateAsync(id, request.Role, request.IsActive, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dto = UserDtoMapper.ToDto(result.Value!);
        return Ok(dto);
    }

    [HttpDelete("users/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeactivateAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("users/{id:guid}/suspend")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SuspendUser(Guid id, [FromBody] SuspendUserRequest request, CancellationToken ct)
    {
        var callerUserId = _identityContext.Value?.InternalUserId;
        if (callerUserId is null)
        {
            return Unauthorized();
        }

        var result = await _userService.SuspendAsync(id, request.Reason, callerUserId.Value, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(UserDtoMapper.ToDto(result.Value!));
    }

    [HttpPost("users/{id:guid}/unsuspend")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnsuspendUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.UnsuspendAsync(id, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return Ok(UserDtoMapper.ToDto(result.Value!));
    }

    [HttpGet("reports")]
    [ProducesResponseType(typeof(List<ViolationReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(CancellationToken ct)
    {
        var result = await _violationReportService.GetAllAsync(ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var dtos = result.Value!.Select(v => new ViolationReportDto(
            v.Id,
            v.ReportedUserId,
            v.ReportedXUsername,
            v.ReportedByUserId,
            v.Explanation,
            v.CreatedAt,
            v.Status,
            v.ReviewedAt)).ToList();

        return Ok(dtos);
    }

    [HttpPost("reports/{id:guid}/dismiss")]
    [ProducesResponseType(typeof(ViolationReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissReport(Guid id, CancellationToken ct)
    {
        var result = await _violationReportService.DismissAsync(id, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var v = result.Value!;
        return Ok(new ViolationReportDto(
            v.Id,
            v.ReportedUserId,
            v.ReportedXUsername,
            v.ReportedByUserId,
            v.Explanation,
            v.CreatedAt,
            v.Status,
            v.ReviewedAt));
    }
}
