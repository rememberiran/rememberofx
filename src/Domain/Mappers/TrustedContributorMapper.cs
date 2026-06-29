using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class TrustedContributorMapper
{
    public static TrustedContributor ToDomain(TrustedContributorRecord record)
    {
        return new TrustedContributor
        {
            OwnerUserId = record.OwnerUserId,
            TrustedXUsername = record.TrustedXUsername,
            CreatedAt = record.CreatedAt,
            OwnerUser = record.OwnerUser is not null ? UserMapper.ToDomain(record.OwnerUser) : null!,
        };
    }

    public static TrustedContributorRecord ToRecord(TrustedContributor entity)
    {
        return new TrustedContributorRecord
        {
            OwnerUserId = entity.OwnerUserId,
            TrustedXUsername = entity.TrustedXUsername,
            CreatedAt = entity.CreatedAt,
        };
    }
}
