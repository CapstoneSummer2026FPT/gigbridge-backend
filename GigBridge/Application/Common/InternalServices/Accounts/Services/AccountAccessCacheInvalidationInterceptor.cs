using System.Collections.Concurrent;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Accounts.Models;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Application.Common.InternalServices.Accounts.Services;

public sealed class AccountAccessCacheInvalidationInterceptor(
    ICacheService cache,
    Microsoft.Extensions.Logging.ILogger<AccountAccessCacheInvalidationInterceptor> logger) : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Guid, Guid[]> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var userIds = context.ChangeTracker.Entries<User>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                    (entry.State is EntityState.Added or EntityState.Deleted ||
                     entry.Property(user => user.IsActive).IsModified ||
                     entry.Property(user => user.AccountStatus).IsModified ||
                     entry.Property(user => user.SuspendedUntil).IsModified))
                .Select(entry => entry.Entity.UserId)
                .Distinct()
                .ToArray();
            if (userIds.Length > 0)
            {
                _pending[eventData.Context.ContextId.InstanceId] = userIds;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context &&
            _pending.TryRemove(context.ContextId.InstanceId, out var userIds))
        {
            foreach (var userId in userIds)
            {
                try
                {
                    await cache.RemoveAsync(AccountAccessCache.Key(userId), cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception,
                        "Account access cache invalidation failed for {UserId}; the entry will expire within {TtlSeconds} seconds.",
                        userId, AccountAccessCache.Duration.TotalSeconds);
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            _pending.TryRemove(context.ContextId.InstanceId, out _);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
