using System.Security.Claims;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Auth.RefreshToken.Commands;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
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
            mapper,
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand("expired-access-token", "old-refresh-token"),
            CancellationToken.None);

        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal("new-hash", user.RefreshTokenHash);
        Assert.Equal(now.AddMinutes(60), user.RefreshTokenExpiry);
        // The just-superseded token is retained for the rotation grace window rather than
        // discarded outright, so a sibling concurrent refresh (e.g. another browser tab) can
        // still succeed instead of being rejected.
        Assert.Equal("old-hash", user.PreviousRefreshTokenHash);
        Assert.NotNull(user.PreviousRefreshTokenGraceExpiresAt);
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
            Substitute.For<IMapper>(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new RefreshTokenCommand("expired-access-token", "stale-refresh-token"),
            CancellationToken.None));

        Assert.Equal(1, context.TransactionLockCount);
        Assert.Equal(0, context.SaveChangesCount);
        Assert.Equal(0, context.TransactionCommitCount);
        Assert.Equal("current-hash", user.RefreshTokenHash);
        jwt.DidNotReceive().GenerateRefreshToken();
    }

    [Fact]
    public async Task Handle_DuplicateCookieCandidates_UsesTheCurrentToken()
    {
        var fixture = new RefreshFixture(UserRole.Client);
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(
                RefreshFixture.AccessToken,
                "stale-legacy-token",
                ["stale-legacy-token", fixture.CurrentRawToken]),
            CancellationToken.None);

        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(fixture.CurrentRawToken, result.RefreshToken);
    }

    /// <summary>
    /// Regression coverage for the refresh-token rotation grace window: a second legitimate
    /// concurrent refresh (e.g. a sibling browser tab presenting the just-rotated-out token)
    /// must succeed instead of being rejected as an invalid token, while a token presented after
    /// the grace window — genuinely stale, reused, or stolen — must still be rejected.
    /// </summary>
    [Fact]
    public async Task Refresh_ConcurrentSiblingRequestWithinGraceWindow_Succeeds()
    {
        // Simulates two browser tabs of the same session racing on the same httpOnly cookie:
        // the first request rotates A -> B; the second, still holding A, must not be rejected.
        var fixture = new RefreshFixture();
        var handler = fixture.CreateHandler();
        var firstTabToken = fixture.CurrentRawToken;

        await handler.Handle(
            new RefreshTokenCommand(RefreshFixture.AccessToken, firstTabToken),
            CancellationToken.None);

        // The sibling tab's request is processed moments later, still within the grace window.
        fixture.AdvanceClock(TimeSpan.FromSeconds(5));

        var (_, secondTabNewToken, _) = await handler.Handle(
            new RefreshTokenCommand(RefreshFixture.AccessToken, firstTabToken),
            CancellationToken.None);

        Assert.NotNull(secondTabNewToken);
        Assert.NotEqual(firstTabToken, secondTabNewToken);
    }

    [Fact]
    public async Task Refresh_WithExpiredCurrentToken_Throws()
    {
        var fixture = new RefreshFixture();
        fixture.User.RefreshTokenExpiry = fixture.Now.AddMinutes(-1);
        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(
                new RefreshTokenCommand(RefreshFixture.AccessToken, fixture.CurrentRawToken),
                CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_SiblingRequestAfterGraceWindowElapsed_Throws()
    {
        var fixture = new RefreshFixture();
        var handler = fixture.CreateHandler();
        var firstTabToken = fixture.CurrentRawToken;

        await handler.Handle(
            new RefreshTokenCommand(RefreshFixture.AccessToken, firstTabToken),
            CancellationToken.None);

        // Past the 30-second grace window — this must still be rejected, proving the fix does
        // not weaken rotation/revocation for a genuinely stale or reused token.
        fixture.AdvanceClock(TimeSpan.FromSeconds(31));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(
                new RefreshTokenCommand(RefreshFixture.AccessToken, firstTabToken),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Freelancer)]
    public async Task Refresh_SucceedsForBothClientAndFreelancer(UserRole role)
    {
        var fixture = new RefreshFixture(role);
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(RefreshFixture.AccessToken, fixture.CurrentRawToken),
            CancellationToken.None);

        Assert.NotNull(result.RefreshToken);
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

    private sealed class RefreshFixture
    {
        public const string AccessToken = "expired-access-token";

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; private set; } = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
        public User User { get; }
        public string CurrentRawToken { get; } = "current-raw-token";

        private readonly IJwtService _jwtService = Substitute.For<IJwtService>();

        public RefreshFixture(UserRole role = UserRole.Client)
        {
            User = new User
            {
                UserId = Guid.NewGuid(),
                FullName = "Test User",
                Email = "user@example.com",
                Role = (int)role,
                IsActive = true,
                AccountStatus = (int)AccountStatus.Active,
                RefreshTokenHash = HashOf(CurrentRawToken),
                RefreshTokenExpiry = Now.AddDays(7),
                CreatedAt = Now
            };
            Context.AddSet(User);

            _jwtService.HashRefreshToken(Arg.Any<string>())
                .Returns(callInfo => HashOf(callInfo.Arg<string>()));
            _jwtService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));
            _jwtService.GenerateToken(Arg.Any<User>()).Returns("new-access-token");
            _jwtService.GetPrincipalFromExpiredToken(Arg.Any<string>())
                .Returns(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, User.UserId.ToString())])));
            _jwtService.GetRefreshTokenExpiryMinutes().Returns(10_080);
        }

        public static string HashOf(string rawToken) => $"hash:{rawToken}";

        public void AdvanceClock(TimeSpan by) => Now = Now.Add(by);

        public RefreshTokenCommandHandler CreateHandler()
        {
            var mapper = Substitute.For<IMapper>();
            mapper.Map<UserDTO>(Arg.Any<User>()).Returns(new UserDTO());

            IDateTimeService dateTimeService = new DynamicDateTimeService(this);

            return new RefreshTokenCommandHandler(
                Context,
                _jwtService,
                dateTimeService,
                mapper,
                NullLogger<RefreshTokenCommandHandler>.Instance);
        }

        private sealed class DynamicDateTimeService : IDateTimeService
        {
            private readonly RefreshFixture _fixture;

            public DynamicDateTimeService(RefreshFixture fixture)
            {
                _fixture = fixture;
            }

            public DateTime UtcNow => _fixture.Now;
        }
    }
}
