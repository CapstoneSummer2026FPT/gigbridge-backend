using Application.Common.InternalServices.Realtime.Models;
using Domain.Entities;
using Domain.Enums.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Application.Common.InternalServices.Realtime.Services;

public sealed record RealtimeRevisionLockTargets(
    IReadOnlySet<Guid> ConversationIds,
    IReadOnlySet<Guid> ReceiptIds,
    IReadOnlySet<Guid> DirectUserIds)
{
    public bool IsEmpty => ConversationIds.Count == 0 && ReceiptIds.Count == 0 && DirectUserIds.Count == 0;
}

public sealed record RealtimeConversationParticipant(Guid ConversationId, Guid UserId);

/// <summary>
/// Central source of truth for the tracked changes that can allocate durable realtime revisions.
/// </summary>
public static class RealtimeRevisionLockTargetDetector
{
    public static RealtimeRevisionLockTargets DetectTrackedTargets(DbContext context)
    {
        var conversationIds = new HashSet<Guid>();
        var receiptIds = new HashSet<Guid>();
        var directUserIds = new HashSet<Guid>();

        AddChangedIds(conversationIds, context.ChangeTracker.Entries<Conversation>(),
            entry => entry.Entity.ConversationsId);
        AddChangedIds(conversationIds, context.ChangeTracker.Entries<ConversationParticipant>(),
            entry => entry.Entity.ConversationsId);
        AddChangedIds(conversationIds, context.ChangeTracker.Entries<Message>(),
            entry => entry.Entity.ConversationsId);
        AddChangedIds(conversationIds, context.ChangeTracker.Entries<NegotiationMilestoneDraft>(),
            entry => entry.Entity.ConversationsId);
        AddChangedIds(conversationIds, context.ChangeTracker.Entries<NegotiationOffer>(),
            entry => entry.Entity.ConversationsId);
        AddChangedIds(receiptIds, context.ChangeTracker.Entries<ProjectReceipt>(),
            entry => entry.Entity.ProjectReceiptId);

        AddChangedIds(directUserIds, context.ChangeTracker.Entries<Notification>(),
            entry => entry.Entity.UserId);
        AddChangedIds(directUserIds, context.ChangeTracker.Entries<BroadcastNotificationRecipient>(),
            entry => entry.Entity.UserId);
        AddChangedIds(directUserIds, context.ChangeTracker.Entries<UserRealtimeState>(),
            entry => entry.Entity.UserId);
        AddChangedIds(directUserIds, context.ChangeTracker.Entries<ConversationParticipant>(),
            entry => entry.Entity.UserId);

        foreach (var entry in context.ChangeTracker.Entries<DeliveryOutbox>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity.DeliveryType is (int)DeliveryOutboxType.NotificationStateRevision or
                (int)DeliveryOutboxType.ConversationInboxRevision)
            {
                directUserIds.Add(entry.Entity.RecipientUserId);
            }
        }

        return new RealtimeRevisionLockTargets(conversationIds, receiptIds, directUserIds);
    }

    public static async Task<IReadOnlyList<RealtimeConversationParticipant>>
        ResolveActiveConversationParticipantsAsync(
            DbContext context,
            IReadOnlySet<Guid> conversationIds,
            CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return [];
        }

        var persisted = await context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(participant =>
                conversationIds.Contains(participant.ConversationsId) &&
                participant.LeftAt == null &&
                participant.DeletedAt == null)
            .Select(participant => new RealtimeConversationParticipant(
                participant.ConversationsId,
                participant.UserId))
            .ToListAsync(cancellationToken);

        var active = persisted
            .GroupBy(participant => (participant.ConversationId, participant.UserId))
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var entry in context.ChangeTracker.Entries<ConversationParticipant>()
                     .Where(entry => conversationIds.Contains(entry.Entity.ConversationsId)))
        {
            var key = (entry.Entity.ConversationsId, entry.Entity.UserId);
            if (entry.State == EntityState.Deleted ||
                entry.Entity.LeftAt.HasValue ||
                entry.Entity.DeletedAt.HasValue)
            {
                active.Remove(key);
                continue;
            }

            active[key] = new RealtimeConversationParticipant(key.ConversationsId, key.UserId);
        }

        return active.Values.ToArray();
    }

    private static void AddChangedIds<TEntity>(
        HashSet<Guid> ids,
        IEnumerable<EntityEntry<TEntity>> entries,
        Func<EntityEntry<TEntity>, Guid> getId)
        where TEntity : class
    {
        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                ids.Add(getId(entry));
            }
        }
    }
}
