using Domain.Entities;
using System.Security.Claims;

namespace Application.Common.Interfaces.IService;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    int GetRefreshTokenExpiryMinutes();
}