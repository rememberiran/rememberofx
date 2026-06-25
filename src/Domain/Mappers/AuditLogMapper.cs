using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class AuditLogMapper
{
    public static AuditLog ToDomain(AuditLogRecord record)
    {
        return new AuditLog
        {
            Id = record.Id,
            CorrelationId = record.CorrelationId,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            PerformedByUserId = record.PerformedByUserId,
            IpAddress = record.IpAddress,
            Region = record.Region,
            Payload = record.Payload,
            CreatedAt = record.CreatedAt,
            PerformedByUser = record.PerformedByUser is not null ? UserMapper.ToDomain(record.PerformedByUser) : null,
        };
    }

    public static AuditLogRecord ToRecord(AuditLog entity)
    {
        return new AuditLogRecord
        {
            Id = entity.Id,
            CorrelationId = entity.CorrelationId,
            Action = entity.Action,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            PerformedByUserId = entity.PerformedByUserId,
            IpAddress = entity.IpAddress,
            Region = entity.Region,
            Payload = entity.Payload,
            CreatedAt = entity.CreatedAt,
        };
    }
}
