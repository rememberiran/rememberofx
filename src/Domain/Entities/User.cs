namespace Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string XUserId { get; set; } = default!;
    public string XUsername { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }
}
