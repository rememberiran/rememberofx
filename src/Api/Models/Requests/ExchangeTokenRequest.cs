using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class ExchangeTokenRequest
{
    [Required]
    public string XAccessToken { get; init; } = default!;
}
