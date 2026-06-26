using Api.Extensions;
using Api.Mappers;
using Api.Models.Requests;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminController(IUserService userService)
    {
        _userService = userService;
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
        Guid? createdByUserId = null;
        var xUserId = User.GetXUserId();
        if (xUserId != null)
        {
            var currentUser = await _userService.GetByXUserIdAsync(xUserId, ct);
            if (currentUser.IsSuccess)
            {
                createdByUserId = currentUser.Value!.Id;
            }
        }

        var result = await _userService.AddAsync(request.XUserId, request.Role, createdByUserId, ct);

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
