using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Services;

public sealed class UserEloAppealResolutionTests
{
    [Fact]
    public async Task FullReversal_NegatesOriginalDelta()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 150, now));
        var original = AddTransaction(context, user.UserId, 40, 110, 150, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.FullReversal, null, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(110, score.CurrentPoints);

        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(-40, correction.PointsDelta);
        Assert.Equal(150, correction.PointsBefore);
        Assert.Equal(110, correction.PointsAfter);
        Assert.Equal((int)EloAdjustmentSourceType.EloAppeal, correction.SourceType);
        Assert.Equal(appeal.EloPointAppealId, correction.EloAppealId);
        Assert.Equal(admin.UserId, correction.AppliedByAdminId);
        Assert.Equal($"elo-appeal-resolution:{appeal.EloPointAppealId}", correction.IdempotencyKey);
    }

    [Fact]
    public async Task PartialCorrection_AppliesGivenDelta()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 150, now));
        var original = AddTransaction(context, user.UserId, 40, 110, 150, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.PartialCorrection, 20, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(170, score.CurrentPoints);
        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(20, correction.PointsDelta);
        Assert.Equal(150, correction.PointsBefore);
        Assert.Equal(170, correction.PointsAfter);
    }

    [Fact]
    public async Task CustomAdjustment_AppliesGivenDelta()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 150, now));
        var original = AddTransaction(context, user.UserId, 40, 110, 150, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.CustomAdjustment, -10, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(140, score.CurrentPoints);
        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(-10, correction.PointsDelta);
    }

    [Fact]
    public async Task Rejected_NoChange_WritesNothing()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 150, now));
        var original = AddTransaction(context, user.UserId, 40, 110, 150, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.NoChange, null, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Null(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(150, score.CurrentPoints);
        Assert.Single(await context.UserEloPointTransactions.ToListAsync());
    }

    [Fact]
    public async Task IsIdempotentPerAppeal()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 150, now));
        var original = AddTransaction(context, user.UserId, 40, 110, 150, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var first = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.FullReversal, null, admin.UserId, CancellationToken.None);
        var second = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.FullReversal, null, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(first);
        Assert.Null(second);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(110, score.CurrentPoints);
        Assert.Equal(2, await context.UserEloPointTransactions.CountAsync());
    }

    [Fact]
    public async Task ClampsAtZeroWhenScoreCannotCoverDelta()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(NewScore(user.UserId, 20, now));
        var original = AddTransaction(context, user.UserId, -30, 50, 20, now);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        var created = await service.ApplyAppealResolutionAsync(
            appeal, EloPointAppealResolution.CustomAdjustment, -100, admin.UserId, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(created);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(0, score.CurrentPoints);
        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(-20, correction.PointsDelta);
        Assert.Equal(20, correction.PointsBefore);
        Assert.Equal(0, correction.PointsAfter);
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

    private static UserEloPointTransaction AddTransaction(
        GigbridgeDbContext context,
        Guid userId,
        int delta,
        int before,
        int after,
        DateTime now)
    {
        var transaction = new UserEloPointTransaction
        {
            UserEloPointTransactionsId = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = delta,
            PointsBefore = before,
            PointsAfter = after,
            Reason = (int)UserEloPointReason.CompletedJobReview,
            SourceEntityType = "Review",
            SourceEntityId = Guid.NewGuid(),
            IdempotencyKey = $"completed-job-review:{Guid.NewGuid()}:{userId}",
            CreatedAt = now
        };
        context.UserEloPointTransactions.Add(transaction);
        return transaction;
    }

    private static EloPointAppeal AddAppeal(
        GigbridgeDbContext context,
        Guid userId,
        Guid transactionId,
        DateTime now)
    {
        var appeal = new EloPointAppeal
        {
            EloPointAppealId = Guid.NewGuid(),
            UserId = userId,
            EloPointTransactionId = transactionId,
            Status = (int)EloPointAppealStatus.Pending,
            Reason = "I believe this change was incorrect.",
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Set<EloPointAppeal>().Add(appeal);
        return appeal;
    }

    private sealed class FixedDateTimeService(DateTime utcNow) : IDateTimeService
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
