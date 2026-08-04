using System.Security.Cryptography;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.ExternalServices.GoogleMeet;
using Infrastructure.Persistence;
using Infrastructure.Services.GoogleMeet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Test_Gigbridge_Backend.Infrastructure.Services.GoogleMeet;

public class GoogleMeetOAuthServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WhenEncryptionKeyIsMissing_ReturnsReconnectRequired()
    {
        var (context, userId) = CreateContextWithActiveConnection();
        await using (context)
        {
            var service = CreateService(context);

            var status = await service.GetStatusAsync(userId, CancellationToken.None);

            Assert.False(status.IsConnected);
            Assert.True(status.NeedsReconnect);
            Assert.Equal("user@example.com", status.GoogleEmail);
            var connection = await context.GoogleMeetConnections.SingleAsync();
            Assert.Equal(GoogleMeetConnectionStatus.ReconnectRequired, connection.Status);
            Assert.Equal("data_protection_key_missing", connection.LastFailureCode);
            Assert.Equal(2, connection.Version);
        }
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenEncryptionKeyIsMissing_RequiresReconnect()
    {
        var (context, userId) = CreateContextWithActiveConnection();
        await using (context)
        {
            var service = CreateService(context);

            var accessToken = await service.GetAccessTokenAsync(userId, CancellationToken.None);

            Assert.Null(accessToken);
            var connection = await context.GoogleMeetConnections.SingleAsync();
            Assert.Equal(GoogleMeetConnectionStatus.ReconnectRequired, connection.Status);
            Assert.Equal("data_protection_key_missing", connection.LastFailureCode);
            Assert.Equal(2, connection.Version);
        }
    }

    private static (GigbridgeDbContext Context, Guid UserId) CreateContextWithActiveConnection()
    {
        var context = new GigbridgeDbContext(
            new DbContextOptionsBuilder<GigbridgeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var userId = Guid.NewGuid();
        context.GoogleMeetConnections.Add(new GoogleMeetConnection
        {
            GoogleMeetConnectionId = Guid.NewGuid(),
            UserId = userId,
            GoogleSubject = "google-subject",
            GoogleEmail = "user@example.com",
            GrantedScopes = "https://www.googleapis.com/auth/meetings.space.created",
            EncryptedRefreshToken = Convert.ToBase64String([1, 2, 3]),
            Status = GoogleMeetConnectionStatus.Active,
            Version = 1,
            ConnectedAt = DateTime.UtcNow
        });
        context.SaveChanges();
        return (context, userId);
    }

    private static GoogleMeetOAuthService CreateService(GigbridgeDbContext context)
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("GoogleMeetOAuth").Returns(new HttpClient());
        var options = Options.Create(new GoogleMeetOptions { ClientId = "client-id" });
        return new GoogleMeetOAuthService(
            context,
            new MissingKeyDataProtectionProvider(),
            httpClientFactory,
            options,
            new GoogleMeetIdTokenValidator(options),
            NullLogger<GoogleMeetOAuthService>.Instance);
    }

    private sealed class MissingKeyDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new MissingKeyDataProtector();
    }

    private sealed class MissingKeyDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) =>
            throw new CryptographicException("The key was not found in the key ring.");
    }
}
