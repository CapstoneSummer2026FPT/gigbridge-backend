using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Freelancer.Purchase;

public sealed class PurchaseSubscriptionCommandHandler(
    IApplicationDbContext context, IWalletLedgerService ledger,
    IDateTimeService clock, ICacheService cache, INotificationService notifications)
    : IRequestHandler<PurchaseSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(
        PurchaseSubscriptionCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            PremiumSubscriptionPolicy.PurchaseLockKey(command.UserId), cancellationToken);

        var existingWalletTransaction = await context.Set<WalletTransaction>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == command.UserId &&
                item.IdempotencyKey == command.Request.IdempotencyKey, cancellationToken);
        var existing = existingWalletTransaction is null ? null :
            await context.Set<Subscription>().AsNoTracking()
                .Include(item => item.SubscriptionPlans)
                .FirstOrDefaultAsync(item => item.UserId == command.UserId &&
                    item.PaymentReference == existingWalletTransaction.WalletTransactionsId.ToString(),
                    cancellationToken);
        if (existing is not null)
        {
            if (existing.SubscriptionPlansId != command.Request.PlanId)
                throw new ConflictException("The idempotency key was already used for another plan.");
            return SubscriptionDto.FromEntity(existing, clock.UtcNow);
        }

        var plan = await context.Set<SubscriptionPlan>()
            .FirstOrDefaultAsync(item => item.SubscriptionPlansId == command.Request.PlanId &&
                item.IsActive == true && item.Price > 0 &&
                (item.TargetRole == null || item.TargetRole == (int)UserRole.Freelancer),
                cancellationToken);
        if (plan is null && command.Request.PlanId == PremiumPlanDefaults.YearlyPromotionPlanId)
        {
            var monthly = await context.Set<SubscriptionPlan>()
                .Where(item => item.IsActive == true && item.TargetRole == (int)UserRole.Freelancer &&
                    item.Price > 0 && item.DurationInDays < 360)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Price)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(
                    "A monthly Freelancer Premium plan is required for the yearly promotion.");
            var monthlyGigCoinPrice = string.Equals(
                monthly.Currency, "VND", StringComparison.OrdinalIgnoreCase)
                ? TokenWalletRules.ToTokensCeiling(monthly.Price)
                : monthly.Price;
            plan = new SubscriptionPlan
            {
                SubscriptionPlansId = PremiumPlanDefaults.YearlyPromotionPlanId,
                Name = "Freelancer Premium Yearly",
                Description = "A full year of Freelancer Premium with two months free",
                Price = monthlyGigCoinPrice * 10,
                Currency = "GigCoin",
                DurationInDays = 365,
                Features = monthly.Features,
                TargetRole = (int)UserRole.Freelancer,
                IsActive = true,
                SortOrder = (monthly.SortOrder ?? 0) + 1,
                CreatedAt = clock.UtcNow
            };
            context.Set<SubscriptionPlan>().Add(plan);
        }

        if (plan is null)
            throw new NotFoundException("Freelancer subscription plan does not exist.");
        if (plan.Price <= 0)
            throw new BadRequestException("Only paid Premium plans can be purchased.");

        var isGigCoin = string.Equals(plan.Currency, "GigCoin", StringComparison.OrdinalIgnoreCase);
        var isLegacyVnd = string.Equals(plan.Currency, "VND", StringComparison.OrdinalIgnoreCase);
        if (!isGigCoin && !isLegacyVnd)
            throw new BadRequestException("This plan uses an unsupported purchase currency.");
        var gigCoinPrice = isLegacyVnd
            ? TokenWalletRules.ToTokensCeiling(plan.Price)
            : plan.Price;

        var walletTransaction = await ledger.DebitAsync(
            command.UserId, gigCoinPrice, WalletTransactionType.SubscriptionPurchase,
            command.Request.IdempotencyKey,
            JsonSerializer.Serialize(new { planId = plan.SubscriptionPlansId, gigCoinPrice }),
            cancellationToken);

        var now = clock.UtcNow;
        var compatible = context.Set<Subscription>()
            .Where(item => item.UserId == command.UserId &&
                           item.Status == SubscriptionStatus.Active &&
                           item.EndDate > now)
            .CompatibleWithRole(UserRole.Freelancer);
        var hasCurrentEntitlement = await compatible
            .AnyAsync(item => item.StartDate <= now, cancellationToken);
        var latestCompatibleEnd = hasCurrentEntitlement
            ? await compatible.OrderByDescending(item => item.EndDate)
                .Select(item => (DateTime?)item.EndDate)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var startsAt = latestCompatibleEnd ?? now;
        var subscription = new Subscription
        {
            SubscriptionsId = Guid.NewGuid(),
            UserId = command.UserId,
            SubscriptionPlansId = plan.SubscriptionPlansId,
            SubscriptionPlans = plan,
            Status = SubscriptionStatus.Active,
            StartDate = startsAt,
            EndDate = startsAt.AddDays(plan.DurationInDays),
            AutoRenew = false,
            PaymentReference = walletTransaction.WalletTransactionsId.ToString(),
            CreatedAt = now
        };
        context.Set<Subscription>().Add(subscription);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await cache.RemoveAsync($"premium:access:{command.UserId:N}", cancellationToken);
        await notifications.CreateNotificationAsync(
            command.UserId, NotificationType.SubscriptionActivated,
            $"Freelancer Premium activated through {subscription.EndDate:yyyy-MM-dd}",
            null, subscription.SubscriptionsId, nameof(Subscription), cancellationToken);

        return SubscriptionDto.FromEntity(subscription, now);
    }
}
