using System.Runtime.CompilerServices;
using Application.Common.InternalServices.WorkSignals.Interfaces;
using Application.Common.InternalServices.WorkSignals.Models;
using Application.Common.Options;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.WorkSignals.Services;

public sealed class WorkSignalSaveChangesInterceptor(IOptions<WorkSignalOptions> options) : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<DbContext, List<string>> PendingChannels = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CapturePendingChannels(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CapturePendingChannels(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishPendingChannelsAsync(eventData.Context, CancellationToken.None)
            .GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PublishPendingChannelsAsync(eventData.Context, cancellationToken);
        return result;
    }

    private void CapturePendingChannels(DbContext? context)
    {
        if (context is null || !options.Value.Enabled)
        {
            return;
        }

        List<string>? channels = null;
        if (context.ChangeTracker.Entries<DeliveryOutbox>()
            .Any(entry => entry.State == EntityState.Added))
        {
            channels = [WorkSignalChannels.DeliveryOutbox];
        }

        if (context.ChangeTracker.Entries<GoogleMeetProvisioningJob>()
            .Any(entry => entry.State == EntityState.Added))
        {
            (channels ??= []).Add(WorkSignalChannels.GoogleMeetProvisioning);
        }

        if (context.ChangeTracker.Entries<ProjectReceipt>()
            .Any(entry => entry.State == EntityState.Added || IsReceiptDueTimeChanged(entry)))
        {
            (channels ??= []).Add(WorkSignalChannels.ProjectReceipt);
        }

        if (channels is not null)
        {
            PendingChannels.AddOrUpdate(context, channels);
        }
    }

    private static bool IsReceiptDueTimeChanged(EntityEntry<ProjectReceipt> entry) =>
        entry.State == EntityState.Modified &&
        (entry.Property(x => x.GenerationStatus).IsModified ||
         entry.Property(x => x.NextGenerationAttemptAt).IsModified ||
         entry.Property(x => x.GenerationLeaseExpiresAt).IsModified ||
         entry.Property(x => x.DeliveryLeaseExpiresAt).IsModified ||
         entry.Property(x => x.NextNotificationAttemptAt).IsModified ||
         entry.Property(x => x.NextEmailAttemptAt).IsModified);

    private static async Task PublishPendingChannelsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !PendingChannels.TryGetValue(context, out var channels))
        {
            return;
        }

        PendingChannels.Remove(context);
        foreach (var channel in channels)
        {
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_notify({channel}, '')",
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Best-effort: the save already committed successfully. A missed NOTIFY just
                // means the consuming worker relies on its deadline-based fallback wake instead.
            }
        }
    }
}
