using Application.Common.InternalServices.Realtime.Models;
using Application.Common.InternalServices.Realtime.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

public partial class GigbridgeDbContext
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesWithRealtimeLocksAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        SaveChangesWithRealtimeLocksAsync(acceptAllChangesOnSuccess, cancellationToken);

    private async Task<int> SaveChangesWithRealtimeLocksAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        if (ChangeTracker.AutoDetectChangesEnabled)
        {
            ChangeTracker.DetectChanges();
        }
        var targets = RealtimeRevisionLockTargetDetector.DetectTrackedTargets(this);
        if (targets.IsEmpty)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        IDbContextTransaction? ownedTransaction = null;
        var transaction = Database.CurrentTransaction;
        if (transaction is null)
        {
            ownedTransaction = await Database.BeginTransactionAsync(cancellationToken);
            transaction = ownedTransaction;
        }

        try
        {
            var resourceLockKeys = RealtimeRevisionLock.OrderDistinct(
                targets.ConversationIds
                    .Select(RealtimeRevisionLock.ForConversation)
                    .Concat(targets.ReceiptIds.Select(RealtimeRevisionLock.ForReceipt)));

            foreach (var lockKey in resourceLockKeys)
            {
                await EfApplicationDbContextTransaction.AcquireTransactionLockAsync(
                    transaction,
                    lockKey,
                    cancellationToken,
                    "RealtimeRevision.Resource");
            }

            // Conversation membership is read only after its resource lock is held. This
            // prevents a participant added by another node from appearing after the set of
            // per-user locks has already been chosen.
            var activeParticipants = await RealtimeRevisionLockTargetDetector
                .ResolveActiveConversationParticipantsAsync(
                    this,
                    targets.ConversationIds,
                    cancellationToken);

            var userLockKeys = RealtimeRevisionLock.OrderDistinct(
                targets.DirectUserIds
                    .Concat(activeParticipants.Select(participant => participant.UserId))
                    .Select(RealtimeRevisionLock.ForUser));

            foreach (var lockKey in userLockKeys)
            {
                await EfApplicationDbContextTransaction.AcquireTransactionLockAsync(
                    transaction,
                    lockKey,
                    cancellationToken,
                    "RealtimeRevision.User");
            }

            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }
}
