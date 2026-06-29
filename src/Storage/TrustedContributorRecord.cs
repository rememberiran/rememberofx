namespace Storage;

public class TrustedContributorRecord
{
    public Guid OwnerUserId { get; set; }
    public string TrustedXUsername { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserRecord OwnerUser { get; set; } = default!;
}
