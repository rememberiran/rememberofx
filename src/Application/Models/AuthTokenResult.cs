namespace Application.Models;

public record AuthTokenResult(string Token, DateTime ExpiresAt);
