using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
namespace Infrastructure.Services.Auth;

public class GoogleAuthService(IConfiguration config) : IGoogleAuthService
{
    private readonly IConfiguration _config = config;
    private readonly HttpClient _httpClient = new();



    public async Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode)
    {
        var clientId = _config["Authentication:Google:ClientId"]?.Trim();
        var clientSecret = _config["Authentication:Google:ClientSecret"]?.Trim();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new Exception("Google Authentication configuration (ClientId or ClientSecret) is missing or empty.");
        }

        // 1️⃣ Exchange auth code → tokens
        var tokenResponse = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "code", authCode },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "redirect_uri", "postmessage" }, // 🔥 REQUIRED for SPA
                { "grant_type", "authorization_code" }
            })
        );

        var error = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Google token exchange failed: {tokenResponse.StatusCode} - {error}");
        }


        var json = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<GoogleTokenResponse>(json)!;

        // 2️⃣ Validate id_token (reuse Google library)
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            tokenData.id_token,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId! }
            }
        );

        return new GoogleUserInfoDTO
        {
            Email = payload.Email,
            Name = payload.Name,
            GoogleId = payload.Subject,
            PictureUrl = payload.Picture
        };
    }



}