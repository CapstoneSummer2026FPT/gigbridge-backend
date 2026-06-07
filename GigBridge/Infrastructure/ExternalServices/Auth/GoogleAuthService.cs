using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.GoogleLogin.DTOs;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Infrastructure.Services.Auth;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public GoogleAuthService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode, CancellationToken cancellationToken = default)
    {
        var clientId = _configuration["Authentication:Google:ClientId"]?.Trim();
        var clientSecret = _configuration["Authentication:Google:ClientSecret"]?.Trim();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Google Authentication configuration (ClientId or ClientSecret) is missing or empty.");
        }

        try
        {
            var tokenResponse = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", authCode },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", "postmessage" },
                    { "grant_type", "authorization_code" }
                }),
                cancellationToken);

            var responseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                throw new BadRequestException($"Google token exchange failed: {tokenResponse.StatusCode} - {responseBody}");
            }

            var tokenData = JsonSerializer.Deserialize<GoogleTokenResponse>(responseBody)
                ?? throw new BadRequestException("Google token response is invalid.");

            var payload = await GoogleJsonWebSignature.ValidateAsync(
                tokenData.id_token,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

            return new GoogleUserInfoDTO
            {
                Email = payload.Email,
                Name = payload.Name,
                GoogleId = payload.Subject,
                PictureUrl = payload.Picture
            };
        }
        catch (Exception ex) when (ex is not BadRequestException)
        {
            throw new BadRequestException($"Google authentication failed: {ex.Message}", ex);
        }
    }
}
