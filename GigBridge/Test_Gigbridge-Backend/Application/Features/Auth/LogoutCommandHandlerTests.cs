using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Auth.Logout.Commands;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth;

public sealed class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_BrowserA_LeavesBrowserBSessionActive()
    {
        var now = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RefreshTokenHash = "hash:browser-a-token",
            RefreshTokenExpiry = now.AddDays(7)
        };
        var browserA = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = "hash:browser-a-token",
            RefreshTokenExpiry = now.AddDays(7),
            CreatedAt = now,
            LastUsedAt = now
        };
        var browserB = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = "hash:browser-b-token",
            RefreshTokenExpiry = now.AddDays(7),
            CreatedAt = now,
            LastUsedAt = now
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var sessions = context.AddSet(browserA, browserB);
        var jwt = Substitute.For<IJwtService>();
        jwt.HashRefreshToken(Arg.Any<string>())
            .Returns(call => $"hash:{call.Arg<string>()}");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now);
        var handler = new LogoutCommandHandler(
            context,
            AuthSessionTestFactory.Create(context, jwt, clock));

        await handler.Handle(
            new LogoutCommand(["browser-a-token"]),
            CancellationToken.None);

        var remaining = Assert.Single(sessions.Entities);
        Assert.Equal(browserB.Id, remaining.Id);
        Assert.Null(user.RefreshTokenHash);
    }

    [Fact]
    public async Task Handle_MatchingLegacyCookie_RevokesEntireRefreshSessionUnderUserLock()
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            PreviousRefreshTokenHash = "legacy-hash",
            PreviousRefreshTokenGraceExpiresAt = DateTime.UtcNow.AddSeconds(20)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var sessions = context.AddSet(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            PreviousRefreshTokenHash = "legacy-hash",
            PreviousRefreshTokenGraceExpiresAt = DateTime.UtcNow.AddSeconds(20),
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        });
        var jwt = Substitute.For<IJwtService>();
        jwt.HashRefreshToken("legacy-token").Returns("legacy-hash");
        var dateTimeService = Substitute.For<IDateTimeService>();
        dateTimeService.UtcNow.Returns(DateTime.UtcNow);
        var handler = new LogoutCommandHandler(
            context,
            AuthSessionTestFactory.Create(context, jwt, dateTimeService));

        await handler.Handle(new LogoutCommand(["legacy-token"]), CancellationToken.None);

        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Null(user.PreviousRefreshTokenHash);
        Assert.Null(user.PreviousRefreshTokenGraceExpiresAt);
        Assert.Empty(sessions.Entities);
        Assert.Equal(1, context.TransactionLockCount);
        Assert.Equal(1, context.SaveChangesCount);
        Assert.Equal(1, context.TransactionCommitCount);
    }

    [Fact]
    public async Task Handle_UnrelatedCookie_DoesNotChangeRefreshSession()
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var sessions = context.AddSet(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        });
        var jwt = Substitute.For<IJwtService>();
        jwt.HashRefreshToken("unrelated-token").Returns("unrelated-hash");
        var dateTimeService = Substitute.For<IDateTimeService>();
        dateTimeService.UtcNow.Returns(DateTime.UtcNow);
        var handler = new LogoutCommandHandler(
            context,
            AuthSessionTestFactory.Create(context, jwt, dateTimeService));

        await handler.Handle(new LogoutCommand(["unrelated-token"]), CancellationToken.None);

        Assert.Equal("current-hash", user.RefreshTokenHash);
        Assert.Single(sessions.Entities);
        Assert.Equal(0, context.TransactionLockCount);
        Assert.Equal(0, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_PreviousCookieOutsideGrace_DoesNotRevokeCurrentSession()
    {
        var now = new DateTime(2026, 8, 29, 13, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = now.AddDays(7),
            PreviousRefreshTokenHash = "stale-hash",
            PreviousRefreshTokenGraceExpiresAt = now.AddSeconds(-1)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var sessions = context.AddSet(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = now.AddDays(7),
            PreviousRefreshTokenHash = "stale-hash",
            PreviousRefreshTokenGraceExpiresAt = now.AddSeconds(-1),
            CreatedAt = now,
            LastUsedAt = now
        });
        var jwt = Substitute.For<IJwtService>();
        jwt.HashRefreshToken("stale-token").Returns("stale-hash");
        var dateTimeService = Substitute.For<IDateTimeService>();
        dateTimeService.UtcNow.Returns(now);
        var handler = new LogoutCommandHandler(
            context,
            AuthSessionTestFactory.Create(context, jwt, dateTimeService));

        await handler.Handle(new LogoutCommand(["stale-token"]), CancellationToken.None);

        Assert.Equal("current-hash", user.RefreshTokenHash);
        Assert.Single(sessions.Entities);
        Assert.Equal(0, context.SaveChangesCount);
    }
}
