using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class UpdateUserRequest
{
    [RegularExpression("^(Admin|Contributor)$")]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}
