using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class UpsertXUserProfileRequest
{
    [MaxLength(200)]
    public string? CustomName { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }
}
