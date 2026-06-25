using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class XUserProfileMapper
{
    public static XUserProfile ToDomain(XUserProfileRecord record)
    {
        return new XUserProfile
        {
            Id = record.Id,
            XUserId = record.XUserId,
            ScrapedUsername = record.ScrapedUsername,
            CustomName = record.CustomName,
            Description = record.Description,
            CreatedByUserId = record.CreatedByUserId,
            UpdatedByUserId = record.UpdatedByUserId,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            CreatedByUser = record.CreatedByUser is not null ? UserMapper.ToDomain(record.CreatedByUser) : null,
            UpdatedByUser = record.UpdatedByUser is not null ? UserMapper.ToDomain(record.UpdatedByUser) : null,
        };
    }

    public static XUserProfileRecord ToRecord(XUserProfile entity)
    {
        return new XUserProfileRecord
        {
            Id = entity.Id,
            XUserId = entity.XUserId,
            ScrapedUsername = entity.ScrapedUsername,
            CustomName = entity.CustomName,
            Description = entity.Description,
            CreatedByUserId = entity.CreatedByUserId,
            UpdatedByUserId = entity.UpdatedByUserId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
