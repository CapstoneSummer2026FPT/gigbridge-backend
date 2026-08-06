using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Services;

public sealed class UserEloAdminAdjustmentTests
{
    [Fact]
    public async Task ApplyAdminAdjustmentAsync_Increase_RecordsLedgerWithAdminAttribution()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 100, now));
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));
        var requestId = Guid.NewGuid();

        var created = await service.ApplyAdminAdjustmentAsync(
            admin.UserId, user.UserId, 50, "Performance bonus", requestId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(150, score.CurrentPoints);

        var transaction = await context.UserEloPointTransactions.SingleAsync();
        Assert.Equal((int)UserEloPointReason.AdminIncrease, transaction.Reason);
        Assert.Equal(100, transaction.PointsBefore);
        Assert.Equal(150, transaction.PointsAfter);
        Assert.Equal((int)EloAdjustmentSourceType.Admin, transaction.SourceType);
        Assert.Equal(admin.UserId, transaction.AppliedByAdminId);
        Assert.Equal(requestId, transaction.SourceEntityId);
        Assert.Equal($"elo-admin:{requestId}", transaction.IdempotencyKey);
        Assert.Equal("Admin", transaction.SourceEntityType);
    }

    [Fact]
    public async Task ApplyAdminAdjustmentAsync_Decrease_ClampsAtZero()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 20, now));
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAdminAdjustmentAsync(
            admin.UserId, user.UserId, -50, null, Guid.NewGuid(), CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(0, score.CurrentPoints);

        var transaction = await context.UserEloPointTransactions.SingleAsync();
        Assert.Equal((int)UserEloPointReason.AdminDecrease, transaction.Reason);
        Assert.Equal(-20, transaction.PointsDelta);
        Assert.Equal(20, transaction.PointsBefore);
        Assert.Equal(0, transaction.PointsAfter);
    }

    [Fact]
    public async Task ApplyAdminAdjustmentAsync_IsIdempotentPerRequestId()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Client, now);
        context.UserEloScores.Add(NewScore(user.UserId, 100, now));
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));
        var requestId = Guid.NewGuid();

        var first = await service.ApplyAdminAdjustmentAsync(
            admin.UserId, user.UserId, 25, null, requestId, CancellationToken.None);
        var second = await service.ApplyAdminAdjustmentAsync(
            admin.UserId, user.UserId, 25, null, requestId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(first);
        Assert.Null(second);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(125, score.CurrentPoints);
        Assert.Single(await context.UserEloPointTransactions.ToListAsync());
    }

    [Fact]
    public async Task ApplyAdminAdjustmentAsync_ThrowsForIneligibleRole()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ApplyAdminAdjustmentAsync(admin.UserId, admin.UserId, 10, null, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAdminAdjustmentAsync_ThrowsWhenUserDoesNotExist()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ApplyAdminAdjustmentAsync(admin.UserId, Guid.NewGuid(), 10, null, Guid.NewGuid(), CancellationToken.None));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static User AddUser(GigbridgeDbContext context, UserRole role, DateTime now)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = $"{role} User",
            Email = $"{Guid.NewGuid():N}@{role.ToString().ToLowerInvariant()}.com",
            Role = (int)role,
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(user);
        return user;
    }

    private static UserEloScore NewScore(Guid userId, int points, DateTime now) => new()
    {
        UserEloScoresId = Guid.NewGuid(),
        UserId = userId,
        CurrentPoints = points,
        LastActivityAt = now,
        CreatedAt = now
    };

    private sealed class FixedDateTimeService(DateTime utcNow) : IDateTimeService
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
