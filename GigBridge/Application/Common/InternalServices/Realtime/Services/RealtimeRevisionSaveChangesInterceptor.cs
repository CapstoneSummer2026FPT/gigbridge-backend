using System.Text.Json;
using Application.Common.InternalServices.Notifications.Models;
using Application.Common.InternalServices.Realtime.Models;
using Domain.Entities;
using Domain.Enums.Delivery;
using Domain.Enums.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Application.Common.InternalServices.Realtime.Services;

public sealed class RealtimeRevisionSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context)
        {
            return result;
        }

        var now = DateTime.UtcNow;
        await QueueNotificationStateChangesAsync(context, now, cancellationToken);
        await QueueConversationStateChangesAsync(context, now, cancellationToken);
        QueueReceiptChanges(context, now);
        return result;
    }

    private static async Task QueueNotificationStateChangesAsync(
        DbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changes = new Dictionary<Guid, int>();
        foreach (var entry in context.ChangeTracker.Entries<Notification>())
        {
            var delta = UnreadDelta(entry, notification => notification.IsRead ?? false);
            if (delta != 0 || entry.State is EntityState.Added or EntityState.Deleted)
                changes[entry.Entity.UserId] = changes.GetValueOrDefault(entry.Entity.UserId) + delta;
        }
        foreach (var entry in context.ChangeTracker.Entries<BroadcastNotificationRecipient>())
        {
            var delta = UnreadDelta(entry, recipient => recipient.IsRead ?? false);
            if (delta != 0 || entry.State is EntityState.Added or EntityState.Deleted)
                changes[entry.Entity.UserId] = changes.GetValueOrDefault(entry.Entity.UserId) + delta;
        }

        foreach (var (userId, delta) in changes)
        {
            var state = await FindOrCreateStateAsync(context, userId, now, cancellationToken);
            state.NotificationRevision++;
            state.NotificationUnreadCount = Math.Max(0, state.NotificationUnreadCount + delta);
            state.UpdatedAt = now;
            var notificationEntries = context.ChangeTracker.Entries<Notification>()
                .Where(entry => entry.Entity.UserId == userId &&
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToArray();
            var hasBroadcastChange = context.ChangeTracker.Entries<BroadcastNotificationRecipient>()
                .Any(entry => entry.Entity.UserId == userId &&
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
            var single = notificationEntries.Length == 1 && !hasBroadcastChange
                ? notificationEntries[0]
                : null;
            var payload = new NotificationStateChangedPayload(
                state.NotificationRevision,
                state.NotificationUnreadCount,
                single is null ? "reset" : single.State == EntityState.Deleted ? "removed" : "upsert",
                single is null || single.State == EntityState.Deleted ? null : ToNotificationDto(single.Entity),
                single?.Entity.NotificationsId);
            AddOutbox(context, userId, DeliveryOutboxType.NotificationStateRevision,
                state.NotificationRevision,
                $"notification-state:{userId:N}:{state.NotificationRevision}", payload, now);
        }
    }

    private static NotificationDto ToNotificationDto(Notification notification) => new()
    {
        Id = notification.NotificationsId,
        Source = "Personal",
        NotificationId = notification.NotificationsId,
        ReadTargetId = notification.NotificationsId,
        Type = (Domain.Enums.Notifications.NotificationType)notification.Type,
        Title = notification.Title,
        Content = notification.Content,
        ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType,
        Metadata = notification.Metadata,
        Revision = notification.Revision,
        IsRead = notification.IsRead ?? false,
        ReadAt = notification.ReadAt,
        CreatedAt = notification.CreatedAt
    };

    private static async Task QueueConversationStateChangesAsync(
        DbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changes = new Dictionary<Guid, (int Delta, HashSet<Guid> Conversations)>();
        var changedConversationIds = new HashSet<Guid>();

        AddChangedConversationIds(
            changedConversationIds,
            context.ChangeTracker.Entries<Conversation>(),
            entry => entry.Entity.ConversationsId);
        AddChangedConversationIds(
            changedConversationIds,
            context.ChangeTracker.Entries<ConversationParticipant>(),
            entry => entry.Entity.ConversationsId);
        AddChangedConversationIds(
            changedConversationIds,
            context.ChangeTracker.Entries<Message>(),
            entry => entry.Entity.ConversationsId);
        AddChangedConversationIds(
            changedConversationIds,
            context.ChangeTracker.Entries<NegotiationMilestoneDraft>(),
            entry => entry.Entity.ConversationsId);
        AddChangedConversationIds(
            changedConversationIds,
            context.ChangeTracker.Entries<NegotiationOffer>(),
            entry => entry.Entity.ConversationsId);

        foreach (var entry in context.ChangeTracker.Entries<ConversationParticipant>())
        {
            if (!changes.TryGetValue(entry.Entity.UserId, out var change))
                change = (0, []);

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                change.Conversations.Add(entry.Entity.ConversationsId);

            if (entry.State == EntityState.Modified &&
                entry.Property(participant => participant.UnreadCount).IsModified)
            {
                var original = entry.Property(participant => participant.UnreadCount).OriginalValue;
                change.Delta += entry.Entity.UnreadCount - original;
            }

            changes[entry.Entity.UserId] = change;
        }

        if (changedConversationIds.Count > 0)
        {
            var persistedParticipants = await context.Set<ConversationParticipant>()
                .AsNoTracking()
                .Where(participant =>
                    changedConversationIds.Contains(participant.ConversationsId) &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null)
                .Select(participant => new { participant.ConversationsId, participant.UserId })
                .ToListAsync(cancellationToken);

            var activeParticipants = persistedParticipants
                .GroupBy(participant => (participant.ConversationsId, participant.UserId))
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var entry in context.ChangeTracker.Entries<ConversationParticipant>()
                         .Where(entry => changedConversationIds.Contains(entry.Entity.ConversationsId)))
            {
                var key = (entry.Entity.ConversationsId, entry.Entity.UserId);
                if (entry.State == EntityState.Deleted ||
                    entry.Entity.LeftAt.HasValue ||
                    entry.Entity.DeletedAt.HasValue)
                {
                    activeParticipants.Remove(key);
                    continue;
                }

                activeParticipants[key] = new
                {
                    entry.Entity.ConversationsId,
                    entry.Entity.UserId
                };
            }

            foreach (var participant in activeParticipants.Values)
            {
                if (!changes.TryGetValue(participant.UserId, out var change))
                    change = (0, []);
                change.Conversations.Add(participant.ConversationsId);
                changes[participant.UserId] = change;
            }
        }

        foreach (var (userId, change) in changes)
        {
            var state = await FindOrCreateStateAsync(context, userId, now, cancellationToken);
            state.ConversationRevision++;
            state.ConversationUnreadCount = Math.Max(0, state.ConversationUnreadCount + change.Delta);
            state.UpdatedAt = now;
            var payload = new ConversationInboxRevisionChangedPayload(
                state.ConversationRevision,
                state.ConversationUnreadCount,
                change.Conversations.Count == 1 ? change.Conversations.Single() : null,
                "upsert");
            AddOutbox(context, userId, DeliveryOutboxType.ConversationInboxRevision,
                state.ConversationRevision,
                $"conversation-inbox:{userId:N}:{state.ConversationRevision}", payload, now);
        }
    }

    private static void AddChangedConversationIds<TEntity>(
        HashSet<Guid> conversationIds,
        IEnumerable<EntityEntry<TEntity>> entries,
        Func<EntityEntry<TEntity>, Guid> getConversationId)
        where TEntity : class
    {
        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                conversationIds.Add(getConversationId(entry));
        }
    }

    private static void QueueReceiptChanges(DbContext context, DateTime now)
    {
        foreach (var entry in context.ChangeTracker.Entries<ProjectReceipt>())
        {
            if (!IsVisibleReceiptChange(entry)) continue;
            entry.Entity.Revision = entry.State == EntityState.Added ? 1 : entry.Entity.Revision + 1;
            entry.Entity.UpdatedAt = now;
            var payload = new ProjectReceiptRevisionChangedPayload(
                entry.Entity.ProjectReceiptId,
                entry.Entity.ContractsId,
                entry.Entity.Revision,
                entry.State == EntityState.Deleted ? "deleted" : "upsert");
            AddOutbox(context, entry.Entity.OwnerUserId, DeliveryOutboxType.ProjectReceiptRevision,
                entry.Entity.Revision,
                $"receipt-revision:{entry.Entity.ProjectReceiptId:N}:{entry.Entity.Revision}:{entry.Entity.OwnerUserId:N}",
                payload, now);
        }
    }

    private static bool IsVisibleReceiptChange(EntityEntry<ProjectReceipt> entry) =>
        entry.State is EntityState.Added or EntityState.Deleted ||
        entry.State == EntityState.Modified && new[]
        {
            nameof(ProjectReceipt.GenerationStatus), nameof(ProjectReceipt.EmailStatus),
            nameof(ProjectReceipt.PdfSizeBytes), nameof(ProjectReceipt.GeneratedAt),
            nameof(ProjectReceipt.EmailedAt)
        }.Any(name => entry.Property(name).IsModified);

    private static int UnreadDelta<TEntity>(EntityEntry<TEntity> entry, Func<TEntity, bool> isRead)
        where TEntity : class
    {
        if (entry.State == EntityState.Added) return isRead(entry.Entity) ? 0 : 1;
        if (entry.State == EntityState.Deleted) return isRead(entry.Entity) ? 0 : -1;
        if (entry.State != EntityState.Modified) return 0;
        var property = entry.Property("IsRead");
        if (!property.IsModified) return 0;
        var before = property.OriginalValue as bool? ?? false;
        var after = property.CurrentValue as bool? ?? false;
        return before == after ? 0 : after ? -1 : 1;
    }

    private static async Task<UserRealtimeState> FindOrCreateStateAsync(
        DbContext context,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var local = context.Set<UserRealtimeState>().Local.FirstOrDefault(state => state.UserId == userId);
        if (local is not null) return local;
        var state = await context.Set<UserRealtimeState>().FindAsync([userId], cancellationToken);
        if (state is not null) return state;
        state = new UserRealtimeState { UserId = userId, UpdatedAt = now };
        context.Set<UserRealtimeState>().Add(state);
        return state;
    }

    private static void AddOutbox<TPayload>(
        DbContext context,
        Guid userId,
        DeliveryOutboxType type,
        int revision,
        string key,
        TPayload payload,
        DateTime now)
    {
        context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = key,
            DeliveryType = (int)type,
            RecipientUserId = userId,
            EventSequence = revision,
            Channel = (int)DeliveryChannel.NotificationRealtime,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }
}
