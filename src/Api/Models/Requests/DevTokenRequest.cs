using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class DevTokenRequest
{
    [Required]
    public string XUserId { get; set; } = default!;
}
