using System.Security.Claims;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Features.Auth.RefreshToken.Commands;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth;

public sealed class RefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidToken_RotatesUnderUserLockAndCommits()
    {
        var now = new DateTime(2026, 8, 23, 16, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            UserId = Guid.NewGuid(),
            IsActive = true,
            RefreshTokenHash = "old-hash",
            RefreshTokenExpiry = now.AddDays(1)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var jwt = CreateJwt(user.UserId);
        jwt.HashRefreshToken("old-refresh-token").Returns("old-hash");
        jwt.GenerateRefreshToken().Returns("new-refresh-token");
        jwt.HashRefreshToken("new-refresh-token").Returns("new-hash");
        jwt.GetRefreshTokenExpiryMinutes().Returns(60);
        jwt.GenerateToken(user).Returns("new-access-token");
        var mapper = Substitute.For<IMapper>();
        mapper.Map<UserDTO>(user).Returns(new UserDTO { UserId = user.UserId });
        var handler = new RefreshTokenCommandHandler(
            context,
            jwt,
            new FixedDateTimeService(now),
            mapper);

        var result = await handler.Handle(
            new RefreshTokenCommand("expired-access-token", "old-refresh-token"),
            CancellationToken.None);

        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal("new-hash", user.RefreshTokenHash);
        Assert.Equal(now.AddMinutes(60), user.RefreshTokenExpiry);
        Assert.Equal(1, context.TransactionBeginCount);
        Assert.Equal(1, context.TransactionLockCount);
        Assert.Equal(AccountEnforcementLock.ForUser(user.UserId), context.LastTransactionLockKey);
        Assert.Equal(1, context.TransactionCommitCount);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_InvalidToken_DoesNotRotateSaveOrCommit()
    {
        var now = new DateTime(2026, 8, 23, 16, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            UserId = Guid.NewGuid(),
            IsActive = true,
            RefreshTokenHash = "current-hash",
            RefreshTokenExpiry = now.AddDays(1)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var jwt = CreateJwt(user.UserId);
        jwt.HashRefreshToken("stale-refresh-token").Returns("stale-hash");
        var handler = new RefreshTokenCommandHandler(
            context,
            jwt,
            new FixedDateTimeService(now),
            Substitute.For<IMapper>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new RefreshTokenCommand("expired-access-token", "stale-refresh-token"),
            CancellationToken.None));

        Assert.Equal(1, context.TransactionLockCount);
        Assert.Equal(0, context.SaveChangesCount);
        Assert.Equal(0, context.TransactionCommitCount);
        Assert.Equal("current-hash", user.RefreshTokenHash);
        jwt.DidNotReceive().GenerateRefreshToken();
    }

    private static IJwtService CreateJwt(Guid userId)
    {
        var jwt = Substitute.For<IJwtService>();
        jwt.GetPrincipalFromExpiredToken("expired-access-token").Returns(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())])));
        return jwt;
    }

    private sealed class FixedDateTimeService(DateTime utcNow) : IDateTimeService
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
