using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.Interfaces.Time;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Auth;

public sealed class AuthSessionServiceTests
{
    [Fact]
    public async Task CreateLoginSession_TwoBrowsersKeepIndependentActiveSessions()
    {
        var now = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
        var user = CreateUser();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var sessions = context.AddSet<AuthSession>();
        var jwt = CreateJwt("browser-a-token", "browser-b-token");
        var service = AuthSessionTestFactory.Create(
            context,
            jwt,
            CreateClock(now));

        var browserA = await service.CreateLoginSessionAsync(user, CancellationToken.None);
        var browserB = await service.CreateLoginSessionAsync(user, CancellationToken.None);

        Assert.Equal("browser-a-token", browserA.Token);
        Assert.Equal("browser-b-token", browserB.Token);
        Assert.Equal(2, sessions.Entities.Count);
        Assert.Contains(sessions.Entities, session => session.RefreshTokenHash == "hash:browser-a-token");
        Assert.Contains(sessions.Entities, session => session.RefreshTokenHash == "hash:browser-b-token");
    }

    [Fact]
    public async Task CreateLoginSession_SixthBrowserRevokesLeastRecentlyUsedSessionOnly()
    {
        var now = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
        var user = CreateUser();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var existingSessions = Enumerable.Range(1, 5)
            .Select(index => new AuthSession
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                RefreshTokenHash = $"hash:token-{index}",
                RefreshTokenExpiry = now.AddDays(7),
                CreatedAt = now.AddDays(-index),
                LastUsedAt = now.AddHours(index - 6)
            })
            .ToArray();
        var sessions = context.AddSet(existingSessions);
        var jwt = CreateJwt("token-6");
        var service = AuthSessionTestFactory.Create(
            context,
            jwt,
            CreateClock(now),
            maximumActiveSessions: 5);

        await service.CreateLoginSessionAsync(user, CancellationToken.None);

        Assert.Equal(5, sessions.Entities.Count);
        Assert.DoesNotContain(sessions.Entities, session => session.RefreshTokenHash == "hash:token-1");
        Assert.Contains(sessions.Entities, session => session.RefreshTokenHash == "hash:token-6");
    }

    [Fact]
    public async Task RotateRefreshSession_RotatesOnlyThePresentingBrowser()
    {
        var now = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
        var user = CreateUser();
        var browserA = CreateSession(user.UserId, "browser-a-token", now);
        var browserB = CreateSession(user.UserId, "browser-b-token", now);
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        context.AddSet(browserA, browserB);
        var jwt = CreateJwt("browser-a-rotated");
        var service = AuthSessionTestFactory.Create(
            context,
            jwt,
            CreateClock(now));

        var rotated = await service.RotateRefreshSessionAsync(
            user,
            ["browser-a-token"],
            CancellationToken.None);

        Assert.Equal("browser-a-rotated", rotated.Token);
        Assert.Equal("hash:browser-a-rotated", browserA.RefreshTokenHash);
        Assert.Equal("hash:browser-a-token", browserA.PreviousRefreshTokenHash);
        Assert.Equal("hash:browser-b-token", browserB.RefreshTokenHash);
        Assert.Null(browserB.PreviousRefreshTokenHash);
    }

    private static User CreateUser() => new()
    {
        UserId = Guid.NewGuid(),
        FullName = "Auth Session User",
        Email = "auth-session@example.com",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    private static AuthSession CreateSession(Guid userId, string token, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        RefreshTokenHash = $"hash:{token}",
        RefreshTokenExpiry = now.AddDays(7),
        CreatedAt = now,
        LastUsedAt = now
    };

    private static IJwtService CreateJwt(params string[] issuedTokens)
    {
        var jwt = Substitute.For<IJwtService>();
        var tokenQueue = new Queue<string>(issuedTokens);
        jwt.GenerateRefreshToken().Returns(_ => tokenQueue.Dequeue());
        jwt.HashRefreshToken(Arg.Any<string>())
            .Returns(call => $"hash:{call.Arg<string>()}");
        jwt.GetRefreshTokenExpiryMinutes().Returns(10_080);
        return jwt;
    }

    private static IDateTimeService CreateClock(DateTime now)
    {
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now);
        return clock;
    }
}
