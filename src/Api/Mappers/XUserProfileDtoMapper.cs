using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public static class XUserProfileDtoMapper
{
    public static XUserProfileDto ToDto(
        XUserProfile profile,
        int archivedTweetCount = 0,
        int totalVotesReceived = 0,
        DateTime? firstArchivedAt = null)
    {
        return new XUserProfileDto(
            profile.Id,
            profile.XUserId,
            profile.XUsername,
            profile.CustomName,
            profile.Description,
            archivedTweetCount,
            totalVotesReceived,
            firstArchivedAt);
    }
}
