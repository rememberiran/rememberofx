using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class UserMapper
{
    public static User ToDomain(UserRecord record)
    {
        return new User
        {
            Id = record.Id,
            XUserId = record.XUserId,
            XUsername = record.XUsername,
            Role = record.Role,
            IsActive = record.IsActive,
            CreatedAt = record.CreatedAt,
            CreatedByUserId = record.CreatedByUserId,
            CreatedByUser = record.CreatedByUser is not null ? ToDomain(record.CreatedByUser) : null,
        };
    }

    public static UserRecord ToRecord(User entity)
    {
        return new UserRecord
        {
            Id = entity.Id,
            XUserId = entity.XUserId,
            XUsername = entity.XUsername,
            Role = entity.Role,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId,
        };
    }
}
