using Application.Common.Interfaces;
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
    public async Task ApplyCompletedJobReview_AppliesOnceAndIsIdempotent()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        var contract = AddContract(context, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));
        var reviewId = Guid.NewGuid();

        // Rating 5.0 -> +50.  Initial grant (100) + 50 = 150.
        await service.ApplyCompletedJobReviewAsync(reviewId, contract.ContractsId, user.UserId, 5m, CancellationToken.None);
        await service.ApplyCompletedJobReviewAsync(reviewId, contract.ContractsId, user.UserId, 5m, CancellationToken.None);
        await context.SaveChangesAsync();

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(150, score.CurrentPoints);

        var transactions = await context.UserEloPointTransactions
            .OrderBy(transaction => transaction.CreatedAt)
            .ThenBy(transaction => transaction.Reason)
            .ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.Equal(2, transactions.Select(transaction => transaction.IdempotencyKey).Distinct().Count());
        Assert.Contains(transactions, transaction => transaction.Reason == (int)UserEloPointReason.InitialGrant);
        Assert.Contains(transactions, transaction => transaction.Reason == (int)UserEloPointReason.CompletedJobReview);
        Assert.Single(transactions.Where(transaction => transaction.Reason == (int)UserEloPointReason.CompletedJobReview));
    }

    [Fact]
    public async Task ApplyCompletedJobReview_NegativeDeltaClampsAtZero()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        var contract = AddContract(context, now);
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

        // Rating 1.0 -> -50 requested, floor clamps the score at 0.
        await service.ApplyCompletedJobReviewAsync(Guid.NewGuid(), contract.ContractsId, user.UserId, 1m, CancellationToken.None);
        await context.SaveChangesAsync();

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(0, score.CurrentPoints);

        var transaction = await context.UserEloPointTransactions.SingleAsync();
        Assert.Equal((int)UserEloPointReason.CompletedJobReview, transaction.Reason);
        Assert.Equal(-20, transaction.PointsDelta);
        Assert.Equal(20, transaction.PointsBefore);
        Assert.Equal(0, transaction.PointsAfter);
        Assert.Equal(1m, transaction.Rating);
    }

    [Fact]
    public async Task ApplyCompletedJobReview_DoesNothingWhenContractIsNotCompleted()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        var contract = AddContract(context, now, status: ContractStatus.Active);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await service.ApplyCompletedJobReviewAsync(Guid.NewGuid(), contract.ContractsId, user.UserId, 5m, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    [Fact]
    public async Task ApplyCompletedJobReview_RejectsInvalidRating()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        var contract = AddContract(context, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        // 3.35 has more than one decimal place -> rejected.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ApplyCompletedJobReviewAsync(Guid.NewGuid(), contract.ContractsId, user.UserId, 3.35m, CancellationToken.None));
        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    [Fact]
    public async Task ApplyCompletedJobReview_RecordsHistoryFields()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var user = AddUser(context, UserRole.Freelancer, now);
        var contract = AddContract(context, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));
        var reviewId = Guid.NewGuid();

        await service.ApplyCompletedJobReviewAsync(reviewId, contract.ContractsId, user.UserId, 3.5m, CancellationToken.None);
        await context.SaveChangesAsync();

        var transaction = await context.UserEloPointTransactions
            .SingleAsync(item => item.Reason == (int)UserEloPointReason.CompletedJobReview);
        Assert.Equal(user.UserId, transaction.UserId);
        Assert.Equal(contract.ContractsId, transaction.ContractId);
        Assert.Equal(reviewId, transaction.ReviewId);
        Assert.Equal(3.5m, transaction.Rating);
        Assert.Equal((int)UserEloPointReason.CompletedJobReview, transaction.Reason);
        Assert.Equal(10, transaction.PointsDelta);
        Assert.Equal(100, transaction.PointsBefore);
        Assert.Equal(110, transaction.PointsAfter);
    }

    [Fact]
    public async Task ApplyCompletedJobReview_SkipsAdminReviewee()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var admin = AddUser(context, UserRole.Admin, now);
        var contract = AddContract(context, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await service.ApplyCompletedJobReviewAsync(Guid.NewGuid(), contract.ContractsId, admin.UserId, 5m, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    [Fact]
    public async Task ApplyCompletedJobReview_ThrowsWhenRevieweeDoesNotExist()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var contract = AddContract(context, now);
        await context.SaveChangesAsync();

        var service = new UserEloService(context, new FixedDateTimeService(now));

        await Assert.ThrowsAsync<global::Application.Common.Exceptions.NotFoundException>(() =>
            service.ApplyCompletedJobReviewAsync(Guid.NewGuid(), contract.ContractsId, Guid.NewGuid(), 5m, CancellationToken.None));
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

    private static Contract AddContract(
        GigbridgeDbContext context,
        DateTime now,
        ContractStatus status = ContractStatus.Completed)
    {
        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = Guid.NewGuid(),
            Title = "Elo review contract",
            TotalBudget = 1000,
            Status = (int)status,
            CompletedAt = status == ContractStatus.Completed ? now : null,
            CreatedAt = now
        };

        context.Contracts.Add(contract);
        return contract;
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
