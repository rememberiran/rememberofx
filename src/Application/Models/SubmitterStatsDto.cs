namespace Application.Models;

public record SubmitterStatsDto(
    int SubmittedTweetCount,
    int TotalVotesEarned,
    int DeletedTweetsPreserved,
    int CreatedFolderCount);
