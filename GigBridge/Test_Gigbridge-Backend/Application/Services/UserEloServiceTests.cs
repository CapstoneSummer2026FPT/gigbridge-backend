using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Services;

public class UserEloServiceTests
{
    [Fact]
    public async Task ApplyReviewScore_IsIdempotentForSameReviewAndReviewee()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));
        var reviewId = Guid.NewGuid();

        await service.ApplyReviewScoreAsync(reviewId, user.UserId, 5, CancellationToken.None);
        await service.ApplyReviewScoreAsync(reviewId, user.UserId, 5, CancellationToken.None);
        await context.SaveChangesAsync();

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(170, score.CurrentPoints);

        var transactions = await context.UserEloPointTransactions
            .OrderBy(transaction => transaction.CreatedAt)
            .ThenBy(transaction => transaction.Reason)
            .ToListAsync();
        Assert.Equal(3, transactions.Count);
        Assert.Equal(3, transactions.Select(transaction => transaction.IdempotencyKey).Distinct().Count());
        Assert.Contains(transactions, transaction => transaction.Reason == (int)UserEloPointReason.InitialGrant);
        Assert.Contains(transactions, transaction => transaction.Reason == (int)UserEloPointReason.JobCompletion);
        Assert.Contains(transactions, transaction => transaction.Reason == (int)UserEloPointReason.ReviewRating);
    }

    [Fact]
    public async Task ApplyReviewScore_ClampsScoreAtZero()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        context.UserEloScores.Add(new UserEloScore
        {
            UserEloScoresId = Guid.NewGuid(),
            UserId = user.UserId,
            CurrentPoints = 20,
            LastActivityAt = now,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await service.ApplyReviewScoreAsync(Guid.NewGuid(), user.UserId, 1, CancellationToken.None);
        await context.SaveChangesAsync();

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(0, score.CurrentPoints);

        var transaction = await context.UserEloPointTransactions.SingleAsync();
        Assert.Equal((int)UserEloPointReason.ReviewRating, transaction.Reason);
        Assert.Equal(-20, transaction.PointsDelta);
        Assert.Equal(20, transaction.PointsBefore);
        Assert.Equal(0, transaction.PointsAfter);
    }

    [Fact]
    public async Task InitializeAndReviewScore_SkipAdminUsers()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await service.InitializeNewUserAsync(admin, CancellationToken.None);
        await service.ApplyReviewScoreAsync(Guid.NewGuid(), admin.UserId, 5, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static User AddUser(
        GigbridgeDbContext context,
        UserRole role,
        DateTime now)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = $"{role} User",
            Email = $"{role.ToString().ToLowerInvariant()}@example.com",
            Role = (int)role,
            IsActive = true,
            CreatedAt = now
        };

        context.Users.Add(user);
        return user;
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
