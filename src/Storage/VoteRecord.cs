namespace Storage;

public class VoteRecord
{
    public Guid Id { get; set; }
    public Guid TweetId { get; set; }
    public string VoterIp { get; set; } = default!;
    public Guid? VoterUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TweetRecord Tweet { get; set; } = default!;
    public UserRecord? VoterUser { get; set; }
}
