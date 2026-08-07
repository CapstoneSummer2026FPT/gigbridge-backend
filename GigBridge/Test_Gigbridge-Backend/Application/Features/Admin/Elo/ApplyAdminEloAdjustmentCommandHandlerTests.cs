using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Admin.Elo.Commands.ApplyAdminEloAdjustment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Elo;

public sealed class ApplyAdminEloAdjustmentCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FixedPointsIncrease_AppliesAdjustment()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var audit = Substitute.For<IAdminAuditService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = CreateHandler(context, audit, notifications);
        var requestId = Guid.NewGuid();

        var result = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.FixedPoints, 50m,
                "Bonus", requestId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(100, result.PointsBefore);
        Assert.Equal(150, result.PointsAfter);
        Assert.Equal((int)UserEloPointReason.AdminIncrease, result.Reason);
        Assert.Equal((int)EloAdjustmentSourceType.Admin, result.SourceType);
        Assert.Equal(admin.UserId, result.AppliedByAdminId);

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(150, score.CurrentPoints);
        audit.Received(1).Add(
            admin.UserId, "Elo.AdminAdjustment", nameof(UserEloPointTransaction),
            Arg.Any<Guid?>(), Arg.Any<object>(), Arg.Any<object>());
        await notifications.Received(1).CreateNotificationAsync(
            user.UserId, NotificationType.EloPointsUpdated,
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            nameof(UserEloPointTransaction), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FixedPointsDecrease_ClampsAtZero()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 20));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, false, EloAdjustmentMode.FixedPoints, 50m, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(20, result.PointsBefore);
        Assert.Equal(0, result.PointsAfter);
        Assert.Equal((int)UserEloPointReason.AdminDecrease, result.Reason);
    }

    [Fact]
    public async Task PercentageIncrease_RoundsHalfAwayFromZero()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 150));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        // 150 * 10% = 15 exactly -> +15. Use 15% of 150 = 22.5 -> away from zero = 23.
        var result = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.Percentage, 15m, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(23, result.PointsDelta);
        Assert.Equal(150, result.PointsBefore);
        Assert.Equal(173, result.PointsAfter);
        Assert.Equal((int)EloAdjustmentMode.Percentage, result.Mode);
    }

    [Fact]
    public async Task PercentageDecrease_MatchesPolicyPenalty()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, false, EloAdjustmentMode.Percentage, 25m, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(-25, result.PointsDelta);
        Assert.Equal(75, result.PointsAfter);
    }

    [Fact]
    public async Task SameRequestId_IsIdempotent()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);
        var requestId = Guid.NewGuid();

        var first = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.FixedPoints, 25m, null, requestId),
            CancellationToken.None);
        var second = await handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.FixedPoints, 25m, null, requestId),
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.TransactionId, second.TransactionId);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(125, score.CurrentPoints);
        Assert.Single(await context.UserEloPointTransactions.ToListAsync());
    }

    [Fact]
    public async Task ZeroComputedIncrease_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        // 1% of 100 = 1 -> not zero; use a fixed tiny delta? A 1% increase yields 1,
        // so instead verify a percentage under the rounding floor on a low score.
        context.UserEloScores.Single().CurrentPoints = 1;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.Percentage, 0.4m, null, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task NoPointsToDeduct_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 0));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, false, EloAdjustmentMode.FixedPoints, 10m, null, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task InvalidAmount_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, user.UserId, true, EloAdjustmentMode.Percentage, 101m, null, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task NonAdmin_ThrowsForbidden()
    {
        await using var context = CreateContext();
        var (_, user) = AddUsers(context);
        context.UserEloScores.Add(NewScore(user.UserId, 100));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                user.UserId, user.UserId, true, EloAdjustmentMode.FixedPoints, 10m, null, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task AdminTarget_ThrowsBadRequest()
    {
        await using var context = CreateContext();
        var (admin, _) = AddUsers(context);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ApplyAdminEloAdjustmentCommand(
                admin.UserId, admin.UserId, true, EloAdjustmentMode.FixedPoints, 10m, null, Guid.NewGuid()),
            CancellationToken.None));
    }

    private static ApplyAdminEloAdjustmentCommandHandler CreateHandler(
        GigbridgeDbContext context,
        IAdminAuditService? audit = null,
        INotificationService? notifications = null)
    {
        var clock = new Clock();
        return new ApplyAdminEloAdjustmentCommandHandler(
            context,
            clock,
            audit ?? Substitute.For<IAdminAuditService>(),
            notifications ?? new NoopNotificationService(),
            new UserEloService(context, clock));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static (User Admin, User User) AddUsers(GigbridgeDbContext context)
    {
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin User",
            Email = $"{Guid.NewGuid():N}@admin.com",
            Role = (int)UserRole.Admin,
            IsActive = true,
            CreatedAt = Now
        };
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer User",
            Email = $"{Guid.NewGuid():N}@freelancer.com",
            Role = (int)UserRole.Freelancer,
            IsActive = true,
            CreatedAt = Now
        };
        context.Users.AddRange(admin, user);
        return (admin, user);
    }

    private static UserEloScore NewScore(Guid userId, int points) => new()
    {
        UserEloScoresId = Guid.NewGuid(),
        UserId = userId,
        CurrentPoints = points,
        LastActivityAt = Now,
        CreatedAt = Now
    };

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }
}
