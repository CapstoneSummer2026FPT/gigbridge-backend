using Domain.Enums.Chat;
using Application.Common.InternalServices.Delivery.BackgroundJobs;
using Domain.Entities;


namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Delivery.BackgroundJobs;

public sealed class DeliveryOutboxTimingTests
{
    [Fact]
    public void RealtimeDeliveryPolling_IsNearInstant()
    {
        Assert.InRange(
            DeliveryOutboxService.DueDeliveryPollInterval,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DueDeliveriesForChannel_IsolatesRealtimeWorkFromEmail()
    {
        var now = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var realtimeDueFirst = Delivery(
            DeliveryChannel.NotificationRealtime, now.AddMilliseconds(-500));
        var realtimeDueSecond = Delivery(
            DeliveryChannel.NotificationRealtime, now);
        var emailDue = Delivery(DeliveryChannel.Email, now.AddSeconds(-1));
        var realtimeFuture = Delivery(
            DeliveryChannel.NotificationRealtime, now.AddMilliseconds(1));

        var due = DeliveryOutboxService.DueDeliveriesForChannel(
                new[]
                {
                    realtimeDueSecond,
                    emailDue,
                    realtimeFuture,
                    realtimeDueFirst
                }.AsQueryable(),
                DeliveryChannel.NotificationRealtime,
                now)
            .ToList();

        Assert.Equal(
            [realtimeDueFirst.DeliveryOutboxId, realtimeDueSecond.DeliveryOutboxId],
            due.Select(x => x.DeliveryOutboxId));
    }

    [Fact]
    public void DueDeliveriesForChannel_UsesIdAsDeterministicTieBreaker()
    {
        var now = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var first = Delivery(DeliveryChannel.Email, now, firstId);
        var second = Delivery(DeliveryChannel.Email, now, secondId);

        var due = DeliveryOutboxService.DueDeliveriesForChannel(
                new[] { second, first }.AsQueryable(),
                DeliveryChannel.Email,
                now)
            .ToList();

        Assert.Equal([firstId, secondId], due.Select(x => x.DeliveryOutboxId));
    }

    private static DeliveryOutbox Delivery(
        DeliveryChannel channel,
        DateTime nextAttemptAt,
        Guid? deliveryOutboxId = null) => new()
    {
        DeliveryOutboxId = deliveryOutboxId ?? Guid.NewGuid(),
        Channel = (int)channel,
        Status = (int)DeliveryOutboxStatus.Pending,
        NextAttemptAt = nextAttemptAt
    };
}
