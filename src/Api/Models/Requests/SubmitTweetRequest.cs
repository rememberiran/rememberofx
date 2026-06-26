using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class SubmitTweetRequest
{
    [Required]
    public string TweetUrl { get; set; } = default!;
    public IReadOnlyList<Guid>? FolderIds { get; set; }
}
