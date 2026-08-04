using Application.Features.Premium.Freelancer.Promotions.Common;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_backend.Application.Features.Premium;

public sealed class PromotionQueueConcurrencyTests
{
    [Fact]
    public async Task RecalculateQueuePositions_ClearsRanksBeforeAssigningUniquePositions()
    {
        var now = DateTime.UtcNow;
        var lowerWeight = Promotion(2m, 1, now.AddMinutes(-2));
        var higherWeight = Promotion(5m, 2, now.AddMinutes(-1));
        var context = new InMemoryApplicationDbContext();
        context.AddSet(lowerWeight, higherWeight);
        var savedPositions = new List<int[]>();
        context.OnSaveChanges = _ =>
            savedPositions.Add([lowerWeight.QueuePosition, higherWeight.QueuePosition]);

        await PromotionPolicy.RecalculateQueuePositionsAsync(
            context, now, CancellationToken.None);

        Assert.Equal(2, context.SaveChangesCount);
        Assert.Equal([0, 0], savedPositions[0]);
        Assert.Equal([2, 1], savedPositions[1]);
    }

    [Fact]
    public async Task PromotionQueueLock_UsesTheSharedTransactionLockKey()
    {
        var context = new InMemoryApplicationDbContext();
        await using var transaction =
            await context.BeginTransactionAsync(CancellationToken.None);

        await transaction.AcquireTransactionLockAsync(
            PromotionPolicy.QueueTransactionLockKey, CancellationToken.None);

        Assert.Equal(1, context.TransactionLockCount);
        Assert.Equal(PromotionPolicy.QueueTransactionLockKey,
            context.LastTransactionLockKey);
    }

    private static FreelancerProfilePromotion Promotion(
        decimal weight,
        int queuePosition,
        DateTime createdAt) => new()
    {
        FreelancerProfilePromotionsId = Guid.NewGuid(),
        BoostWeight = weight,
        QueuePosition = queuePosition,
        CreatedAt = createdAt,
        StartTime = createdAt.AddMinutes(-1),
        EndTime = createdAt.AddDays(1),
        Status = PromotionStatus.Active
    };
}
