using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public static class XUserProfileDtoMapper
{
    public static XUserProfileDto ToDto(XUserProfile profile)
    {
        return new XUserProfileDto(
            profile.Id,
            profile.XUserId,
            profile.XUsername,
            profile.CustomName,
            profile.Description);
    }
}
