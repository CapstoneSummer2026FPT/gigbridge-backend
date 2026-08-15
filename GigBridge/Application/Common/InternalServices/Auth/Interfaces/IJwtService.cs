using Domain.Entities;
using System.Security.Claims;

namespace Application.Common.InternalServices.Auth.Interfaces;
public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    int GetRefreshTokenExpiryMinutes();
}