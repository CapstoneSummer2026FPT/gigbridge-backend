namespace Application.Common.InternalServices.Auth.Models;

public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt);
