using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public static class UserDtoMapper
{
    public static UserDto ToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.XUserId,
            user.XUsername,
            user.Role,
            user.IsActive,
            user.CreatedAt);
    }
}
