using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class AddTrustedContributorRequest
{
    [Required]
    [MaxLength(100)]
    public string XUsername { get; set; } = default!;
}
