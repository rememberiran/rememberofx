namespace Domain.Entities;

public class Vote
{
    public Guid Id { get; set; }
    public Guid TweetId { get; set; }
    public string VoterIp { get; set; } = default!;
    public Guid? VoterUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tweet Tweet { get; set; } = default!;
    public User? VoterUser { get; set; }
}
