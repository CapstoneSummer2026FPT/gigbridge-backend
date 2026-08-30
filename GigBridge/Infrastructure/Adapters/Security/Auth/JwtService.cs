using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common.InternalServices.Auth.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Adapters.Security.Auth;

public class JwtService : IJwtService
{
    private readonly string _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenMinutes;

    public JwtService(IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        _signingKey = GetRequiredSetting(jwtSettings, "Key");
        _issuer = GetRequiredSetting(jwtSettings, "Issuer");
        _audience = GetRequiredSetting(jwtSettings, "Audience");
        _accessTokenMinutes = GetRequiredPositiveMinutes(jwtSettings, "AccessTokenMinutes");
        _refreshTokenMinutes = GetRequiredPositiveMinutes(jwtSettings, "RefreshTokenMinutes");

        if (Encoding.UTF8.GetByteCount(_signingKey) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes.");
        }
    }

    public string GenerateToken(User user)
    {
        var secretKey = Encoding.UTF8.GetBytes(_signingKey);
        var issuedAt = DateTime.UtcNow;

        string roleName = user.Role.ToUserRole().ToString();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt).ToString(),
                ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(secretKey);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(_accessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var secretKey = Encoding.UTF8.GetBytes(_signingKey);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateLifetime = false // Ignore token expiration
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Invalid access token");
        }

        return principal;
    }

    public int GetRefreshTokenExpiryMinutes()
    {
        return _refreshTokenMinutes;
    }

    private static string GetRequiredSetting(IConfigurationSection jwtSettings, string key)
    {
        var value = jwtSettings[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Jwt:{key} must be configured.");
        }

        return value;
    }

    private static int GetRequiredPositiveMinutes(
        IConfigurationSection jwtSettings,
        string key)
    {
        var configuredValue = GetRequiredSetting(jwtSettings, key);
        if (!int.TryParse(configuredValue, out var minutes) || minutes <= 0)
        {
            throw new InvalidOperationException($"Jwt:{key} must be a positive integer.");
        }

        return minutes;
    }
}
