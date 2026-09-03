using Application.Common.InternalServices.Realtime.Models;
using Application.Common.InternalServices.Realtime.Services;
using Domain.Entities;
using Domain.Enums.Delivery;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Realtime;

public sealed class RealtimeRevisionLockTargetDetectorTests
{
    [Fact]
    public void LockKeys_AreStableAndResourceScoped()
    {
        var id = Guid.NewGuid();

        Assert.Equal(RealtimeRevisionLock.ForUser(id), RealtimeRevisionLock.ForUser(id));
        Assert.NotEqual(RealtimeRevisionLock.ForUser(id), RealtimeRevisionLock.ForConversation(id));
        Assert.NotEqual(RealtimeRevisionLock.ForUser(id), RealtimeRevisionLock.ForReceipt(id));
        Assert.NotEqual(RealtimeRevisionLock.ForConversation(id), RealtimeRevisionLock.ForReceipt(id));
    }

    [Fact]
    public void OrderDistinct_SortsAndDeduplicatesLockKeys()
    {
        Assert.Equal([-4L, 2L, 9L], RealtimeRevisionLock.OrderDistinct([9L, -4L, 2L, 9L, -4L]));
    }

    [Fact]
    public void DetectTrackedTargets_DeduplicatesAllRealtimeSources()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        context.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        context.Notifications.Add(new Notification
        {
            NotificationsId = Guid.NewGuid(),
            UserId = userId,
            Title = "Changed",
            CreatedAt = DateTime.UtcNow
        });
        context.DeliveryOutboxes.Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            RecipientUserId = userId,
            DeliveryType = (int)DeliveryOutboxType.ConversationInboxRevision
        });
        context.ProjectReceipts.Add(new ProjectReceipt { ProjectReceiptId = receiptId });

        var targets = RealtimeRevisionLockTargetDetector.DetectTrackedTargets(context);

        Assert.Equal([conversationId], targets.ConversationIds);
        Assert.Equal([receiptId], targets.ReceiptIds);
        Assert.Equal([userId], targets.DirectUserIds);
    }

    [Fact]
    public void DetectTrackedTargets_NonRealtimeChangesAreIgnored()
    {
        using var context = CreateContext();
        context.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Unrelated user",
            Email = "unrelated@example.test"
        });

        var targets = RealtimeRevisionLockTargetDetector.DetectTrackedTargets(context);

        Assert.True(targets.IsEmpty);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase($"realtime-lock-targets-{Guid.NewGuid():N}")
            .Options;
        return new GigbridgeDbContext(options);
    }
}
