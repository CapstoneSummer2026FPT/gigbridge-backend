using Application.Features.Chat.Common.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Chat;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Google.Meet;

internal sealed class GoogleMeetOAuthService : IGoogleMeetOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly HttpClient _httpClient;
    private readonly GoogleMeetOptions _options;
    private readonly GoogleMeetIdTokenValidator _idTokenValidator;
    private readonly ILogger<GoogleMeetOAuthService> _logger;

    public GoogleMeetOAuthService(
        IApplicationDbContext context,
        IDataProtectionProvider dataProtection,
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleMeetOptions> options,
        GoogleMeetIdTokenValidator idTokenValidator,
        ILogger<GoogleMeetOAuthService> logger)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("GoogleMeetOAuth");
        _httpClient = httpClientFactory.CreateClient("GoogleMeetOAuth");
        _options = options.Value;
        _idTokenValidator = idTokenValidator;
        _logger = logger;
    }

    public async Task<AuthorizationUrlResult> GetAuthorizationUrlAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stateBytes = new byte[32];
        var nonceBytes = new byte[32];
        var verifierBytes = new byte[32];
        RandomNumberGenerator.Fill(stateBytes);
        RandomNumberGenerator.Fill(nonceBytes);
        RandomNumberGenerator.Fill(verifierBytes);

        var state = Convert.ToHexString(stateBytes).ToLowerInvariant();
        var nonce = Convert.ToHexString(nonceBytes).ToLowerInvariant();
        var codeVerifier = Convert.ToHexString(verifierBytes).ToLowerInvariant();
        var codeChallenge = Convert.ToBase64String(
                SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var stateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        var nonceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));
        var flowId = Guid.NewGuid();

        var oauthState = new GoogleMeetOAuthState
        {
            GoogleMeetOAuthStateId = Guid.NewGuid(),
            UserId = userId,
            StateHash = stateHash,
            NonceHash = nonceHash,
            ProtectedCodeVerifier = ProtectString(codeVerifier),
            FlowId = flowId,
            FrontendReturnPath = "/integrations/google-meet/callback",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<GoogleMeetOAuthState>().Add(oauthState);
        await _context.SaveChangesAsync(cancellationToken);

        var scopes = Uri.EscapeDataString("openid email https://www.googleapis.com/auth/meetings.space.created");
        var redirectUri = Uri.EscapeDataString(_options.BackendCallbackUri);

        var url = $"{_options.AuthorizationEndpoint}?" +
                  $"response_type=code&" +
                  $"client_id={Uri.EscapeDataString(_options.ClientId)}&" +
                  $"redirect_uri={redirectUri}&" +
                  $"scope={scopes}&" +
                  $"state={state}&" +
                  $"nonce={nonce}&" +
                  $"code_challenge={codeChallenge}&" +
                  $"code_challenge_method=S256&" +
                  $"access_type=offline&" +
                  $"include_granted_scopes=true&" +
                  $"prompt=consent";

        return new AuthorizationUrlResult(url, oauthState.ExpiresAt, flowId);
    }

    public async Task<string> HandleCallbackAsync(
        Guid userId,
        string state,
        string? code,
        string? error,
        CancellationToken cancellationToken)
    {
        var stateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));

        var oauthState = await _context.Set<GoogleMeetOAuthState>()
            .Where(s => s.StateHash == stateHash && s.UserId == userId && s.ConsumedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);

        if (oauthState is null)
            return "invalid_state";

        oauthState.ConsumedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(error))
        {
            await _context.SaveChangesAsync(cancellationToken);
            return "cancelled";
        }

        if (string.IsNullOrEmpty(code))
        {
            await _context.SaveChangesAsync(cancellationToken);
            return "invalid_request";
        }

        try
        {
            var codeVerifier = UnprotectString(oauthState.ProtectedCodeVerifier);

            var tokenResponse = await _httpClient.PostAsync(
                _options.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", _options.ClientId },
                    { "client_secret", _options.ClientSecret },
                    { "redirect_uri", _options.BackendCallbackUri },
                    { "grant_type", "authorization_code" },
                    { "code_verifier", codeVerifier }
                }),
                cancellationToken);

            var responseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token exchange failed with status {Status}", tokenResponse.StatusCode);
                await _context.SaveChangesAsync(cancellationToken);
                return "token_exchange_failed";
            }

            var tokenData = JsonSerializer.Deserialize<GoogleTokenExchangeResponse>(responseBody, JsonOptions);
            if (tokenData is null || string.IsNullOrEmpty(tokenData.RefreshToken))
            {
                await _context.SaveChangesAsync(cancellationToken);
                return "missing_refresh_token";
            }

            var idToken = await _idTokenValidator.ValidateAsync(tokenData.IdToken, oauthState.NonceHash);
            if (idToken is null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                return "invalid_id_token";
            }

            var encryptedRefreshToken = ProtectString(tokenData.RefreshToken);
            var scopes = string.Join(" ", tokenData.Scope?.Split(' ') ?? Array.Empty<string>());

            if (!scopes.Contains("meetings.space.created"))
            {
                await _context.SaveChangesAsync(cancellationToken);
                return "missing_meet_scope";
            }

            // Disconnect the previous active connection in one transaction
            var previousConnections = await _context.Set<GoogleMeetConnection>()
                .Where(c => c.UserId == userId && c.DisconnectedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var prev in previousConnections)
            {
                prev.Status = GoogleMeetConnectionStatus.Disconnected;
                prev.DisconnectedAt = DateTime.UtcNow;
            }

            var connection = new GoogleMeetConnection
            {
                GoogleMeetConnectionId = Guid.NewGuid(),
                UserId = userId,
                GoogleSubject = idToken.Subject,
                GoogleEmail = idToken.Email,
                GrantedScopes = scopes,
                EncryptedRefreshToken = encryptedRefreshToken,
                Status = GoogleMeetConnectionStatus.Active,
                Version = 1,
                ConnectedAt = DateTime.UtcNow
            };

            _context.Set<GoogleMeetConnection>().Add(connection);
            await _context.SaveChangesAsync(cancellationToken);

            return "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback failed for user {UserId}", userId);
            await _context.SaveChangesAsync(cancellationToken);
            return "internal_error";
        }
    }

    public async Task<GoogleMeetConnectionStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var connection = await _context.Set<GoogleMeetConnection>()
            .Where(c => c.UserId == userId && c.DisconnectedAt == null)
            .OrderByDescending(c => c.ConnectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
            return new GoogleMeetConnectionStatusResponse(false, null, null, false);

        if (connection.Status == GoogleMeetConnectionStatus.Active)
        {
            try
            {
                _ = UnprotectString(connection.EncryptedRefreshToken);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                await MarkReconnectRequiredAsync(connection, userId, ex, cancellationToken);
            }
        }

        return new GoogleMeetConnectionStatusResponse(
            connection.Status == GoogleMeetConnectionStatus.Active,
            connection.GoogleEmail,
            connection.ConnectedAt,
            connection.Status == GoogleMeetConnectionStatus.ReconnectRequired);
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await _context.Set<GoogleMeetConnection>()
            .Where(c => c.UserId == userId && c.DisconnectedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
            return;

        // Revoke token best-effort
        try
        {
            var refreshToken = UnprotectString(connection.EncryptedRefreshToken);
            await _httpClient.PostAsync(
                _options.RevocationEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "token", refreshToken }
                }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token revocation failed for user {UserId} (best-effort)", userId);
        }

        connection.Status = GoogleMeetConnectionStatus.Disconnected;
        connection.DisconnectedAt = DateTime.UtcNow;
        connection.Version++;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await _context.Set<GoogleMeetConnection>()
            .Where(c => c.UserId == userId && c.DisconnectedAt == null)
            .OrderByDescending(c => c.ConnectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null || connection.Status != GoogleMeetConnectionStatus.Active)
            return null;

        string refreshToken;
        try
        {
            refreshToken = UnprotectString(connection.EncryptedRefreshToken);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            await MarkReconnectRequiredAsync(connection, userId, ex, cancellationToken);
            return null;
        }

        try
        {
            var tokenResponse = await _httpClient.PostAsync(
                _options.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "client_id", _options.ClientId },
                    { "client_secret", _options.ClientSecret },
                    { "grant_type", "refresh_token" }
                }),
                cancellationToken);

            var responseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorData = JsonSerializer.Deserialize<GoogleTokenErrorResponse>(responseBody, JsonOptions);
                if (errorData?.Error == "invalid_grant")
                {
                    connection.Status = GoogleMeetConnectionStatus.ReconnectRequired;
                    connection.LastFailureCode = "invalid_grant";
                    connection.Version++;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return null;
            }

            var tokenData = JsonSerializer.Deserialize<GoogleTokenExchangeResponse>(responseBody, JsonOptions);
            if (tokenData is null)
                return null;

            connection.LastRefreshedAt = DateTime.UtcNow;

            // If Google returns a new refresh token, replace it with optimistic concurrency
            if (!string.IsNullOrEmpty(tokenData.RefreshToken))
            {
                connection.EncryptedRefreshToken = ProtectString(tokenData.RefreshToken);
                connection.Version++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return tokenData.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed for user {UserId}", userId);
            return null;
        }
    }

    private string ProtectString(string plaintext)
    {
        return Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(plaintext)));
    }

    private string UnprotectString(string protectedText)
    {
        return Encoding.UTF8.GetString(_protector.Unprotect(Convert.FromBase64String(protectedText)));
    }

    private async Task MarkReconnectRequiredAsync(
        GoogleMeetConnection connection,
        Guid userId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        connection.Status = GoogleMeetConnectionStatus.ReconnectRequired;
        connection.LastFailureCode = "data_protection_key_missing";
        connection.Version++;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            exception,
            "Stored Google Meet token could not be decrypted for user {UserId}; reconnection is required",
            userId);
    }

    private record GoogleTokenExchangeResponse(
        string? AccessToken,
        string? IdToken,
        string? RefreshToken,
        int? ExpiresIn,
        string? Scope,
        string? TokenType);

    private record GoogleTokenErrorResponse(string? Error, string? ErrorDescription);
}
