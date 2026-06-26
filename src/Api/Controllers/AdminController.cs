using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAsyncContext<IdentityContext> _identityContext;

    public AdminController(IUserService userService, IAsyncContext<IdentityContext> identityContext)
    {
        _userService = userService;
        _identityContext = identityContext;
    }

    [HttpGet("users")]
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
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeactivateAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
