using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class AddUserRequest
{
    [Required]
    public string XUserId { get; set; } = default!;

    [Required]
    [RegularExpression($"^(Admin|Contributor)$")]
    public string Role { get; set; } = default!;
}
