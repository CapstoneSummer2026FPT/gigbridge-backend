using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Services.Payments;
using Domain.Entities;
using Domain.Enums;
using Application.Features.Premium.Freelancer.Promotions.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class PremiumExpiryWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PremiumExpiryWorker> _logger;

    public PremiumExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PremiumExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await ExpireRankProtectionAsync(stoppingToken);
                await AdvancePromotionQueuesAsync(stoppingToken);
                await RenewSubscriptionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Premium expiry processing failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireRankProtectionAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var now = DateTime.UtcNow;

        var expired = await context.Set<FreelancerRankProtection>()
            .Include(item => item.FreelancerProfile)
            .Where(item => item.IsVacationModeEnabled &&
                           item.CancelledAt == null &&
                           item.RankProtectionEndsAt <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return;

        foreach (var item in expired)
            item.IsVacationModeEnabled = false;
        await context.SaveChangesAsync(cancellationToken);

        foreach (var item in expired)
        {
            var userId = item.FreelancerProfile.UserId;
            await cache.RemoveAsync($"premium:rank-protection:{userId:N}", cancellationToken);
            await notifications.CreateNotificationAsync(
                userId, NotificationType.RankProtectionExpired,
                "Vacation Mode expired", "Your ranking protection period has ended.",
                item.FreelancerRankProtectionsId, nameof(FreelancerRankProtection),
                cancellationToken);
        }
    }

    private async Task AdvancePromotionQueuesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var now = DateTime.UtcNow;

        var expired = await context.Set<FreelancerProfilePromotion>()
            .Include(item => item.FreelancerProfile)
            .Where(item => item.Status == PromotionStatus.Active && item.EndTime <= now)
            .ToListAsync(cancellationToken);

        foreach (var active in expired)
        {
            active.Status = PromotionStatus.Expired;
            active.ExpiredAt = now;
            var pending = await context.Set<FreelancerProfilePromotion>()
                .Where(item => item.FreelancerProfileId == active.FreelancerProfileId &&
                               item.Status == PromotionStatus.Pending)
                .OrderBy(item => item.StartTime)
                .ToListAsync(cancellationToken);
            if (pending.Count > 0)
            {
                var cursor = now;
                for (var index = 0; index < pending.Count; index++)
                {
                    var item = pending[index];
                    item.StartTime = cursor;
                    item.EndTime = cursor.AddDays(item.DurationDays);
                    if (index == 0)
                    {
                        item.Status = PromotionStatus.Active;
                        item.ActivatedAt = now;
                    }
                    cursor = item.EndTime;
                }
            }
        }

        if (expired.Count == 0)
            return;
        await context.SaveChangesAsync(cancellationToken);
        await PromotionPolicy.RecalculateQueuePositionsAsync(
            context, now, cancellationToken);

        foreach (var active in expired)
        {
            var userId = active.FreelancerProfile.UserId;
            await cache.RemoveAsync(PromotionPolicy.UserCacheKey(userId), cancellationToken);
            await cache.RemoveAsync(PromotionPolicy.FeedCacheKey, cancellationToken);
            await notifications.CreateNotificationAsync(
                userId, NotificationType.PromotionExpired, "Profile promotion expired",
                $"Your {active.PackageName} promotion has ended.",
                active.FreelancerProfilePromotionsId, nameof(FreelancerProfilePromotion),
                cancellationToken);

            var next = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.FreelancerProfileId == active.FreelancerProfileId &&
                    item.Status == PromotionStatus.Active, cancellationToken);
            if (next is not null)
                await notifications.CreateNotificationAsync(
                    userId, NotificationType.PromotionActivated, "Promotion activated",
                    $"Your profile is promoted until {next.EndTime:O}.",
                    next.FreelancerProfilePromotionsId, nameof(FreelancerProfilePromotion),
                    cancellationToken);
        }
    }

    private async Task RenewSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();
        var now = DateTime.UtcNow;

        var due = await context.Set<Subscription>()
            .Include(item => item.SubscriptionPlans)
            .Include(item => item.User)
            .Where(item => item.Status == SubscriptionStatus.Active &&
                           item.AutoRenew == true &&
                           item.EndDate <= now &&
                           item.SubscriptionPlans.IsActive == true &&
                           item.SubscriptionPlans.Price > 0)
            .ToListAsync(cancellationToken);

        foreach (var subscription in due)
        {
            try
            {
                await using var transaction = await context.BeginTransactionAsync(cancellationToken);
                var isLegacyVnd = string.Equals(subscription.SubscriptionPlans.Currency, "VND", StringComparison.OrdinalIgnoreCase);
                var tokenPrice = isLegacyVnd
                    ? TokenWalletRules.ToTokensCeiling(subscription.SubscriptionPlans.Price)
                    : subscription.SubscriptionPlans.Price;
                var idempotencyKey = $"premium-auto-renew:{subscription.SubscriptionsId:N}:{subscription.EndDate:O}";
                var walletTransaction = await ledger.DebitAsync(
                    subscription.UserId, tokenPrice, WalletTransactionType.SubscriptionPurchase,
                    idempotencyKey,
                    JsonSerializer.Serialize(new { subscriptionId = subscription.SubscriptionsId, autoRenew = true }),
                    cancellationToken);

                subscription.AutoRenew = false;
                subscription.UpdatedAt = now;
                var startsAt = now;
                var renewed = new Subscription
                {
                    SubscriptionsId = Guid.NewGuid(),
                    UserId = subscription.UserId,
                    SubscriptionPlansId = subscription.SubscriptionPlansId,
                    SubscriptionPlans = subscription.SubscriptionPlans,
                    Status = SubscriptionStatus.Active,
                    StartDate = startsAt,
                    EndDate = startsAt.AddDays(subscription.SubscriptionPlans.DurationInDays),
                    AutoRenew = true,
                    PaymentReference = walletTransaction.WalletTransactionsId.ToString(),
                    CreatedAt = now
                };
                context.Set<Subscription>().Add(renewed);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var isClient = subscription.User.Role == (int)UserRole.Client;
                var cacheKey = isClient
                    ? $"premium:access:client:{subscription.UserId:N}"
                    : $"premium:access:{subscription.UserId:N}";
                var premiumName = isClient ? "Client Premium" : "Freelancer Premium";
                await cache.RemoveAsync(cacheKey, cancellationToken);
                await notifications.CreateNotificationAsync(
                    subscription.UserId, NotificationType.SubscriptionActivated,
                    $"{premiumName} auto-renewed through {renewed.EndDate:yyyy-MM-dd}",
                    null, renewed.SubscriptionsId, nameof(Subscription), cancellationToken);
            }
            catch (Exception exception)
            {
                subscription.AutoRenew = false;
                subscription.UpdatedAt = now;
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(exception, "Could not auto-renew Premium subscription {SubscriptionId}", subscription.SubscriptionsId);
                await notifications.CreateNotificationAsync(
                    subscription.UserId, NotificationType.SubscriptionCancelled,
                    "Premium auto-renewal could not be completed",
                    "Auto-renew has been turned off. Please top up your GigCoin wallet and renew manually.",
                    subscription.SubscriptionsId, nameof(Subscription), cancellationToken);
            }
        }
    }
}
