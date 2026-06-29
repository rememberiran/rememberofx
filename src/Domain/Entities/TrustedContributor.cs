namespace Domain.Entities;

public class TrustedContributor
{
    public Guid OwnerUserId { get; set; }
    public string TrustedXUsername { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User OwnerUser { get; set; } = default!;
}
