using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Wallets.Common.Interfaces;
using Application.Features.Premium.Client.Subscriptions.DTOs;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Notifications;
using Domain.Enums.Subscriptions;
using Domain.Enums.Wallets;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.Purchase;

public sealed class PurchaseClientSubscriptionCommandHandler(
    IApplicationDbContext context, IWalletLedgerService ledger, IDateTimeService clock,
    ICacheService cache, INotificationService notifications)
    : IRequestHandler<PurchaseClientSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(
        PurchaseClientSubscriptionCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            PremiumSubscriptionPolicy.PurchaseLockKey(command.UserId), cancellationToken);

        var walletEntry = await context.Set<WalletTransaction>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == command.UserId &&
                item.IdempotencyKey == command.Request.IdempotencyKey, cancellationToken);
        var existing = walletEntry is null ? null : await context.Set<Subscription>().AsNoTracking()
            .Include(item => item.SubscriptionPlans)
            .FirstOrDefaultAsync(item => item.UserId == command.UserId &&
                item.PaymentReference == walletEntry.WalletTransactionsId.ToString(), cancellationToken);
        if (existing is not null)
        {
            if (existing.SubscriptionPlansId != command.Request.PlanId)
                throw new ConflictException("The idempotency key was already used for another plan.");
            return SubscriptionDto.FromEntity(existing, clock.UtcNow);
        }

        var plan = await context.Set<SubscriptionPlan>()
            .FirstOrDefaultAsync(item => item.SubscriptionPlansId == command.Request.PlanId &&
                item.IsActive == true && item.Price > 0 &&
                (item.TargetRole == null || item.TargetRole == (int)UserRole.Client), cancellationToken);
        if (plan is null && (command.Request.PlanId == ClientSubscriptionPlanDefaults.MonthlyPlanId ||
            command.Request.PlanId == ClientSubscriptionPlanDefaults.YearlyPlanId))
        {
            var template = await context.Set<SubscriptionPlan>().AsNoTracking()
                .Where(item => item.IsActive == true && item.Price > 0 && item.DurationInDays < 360)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Price)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("A paid monthly plan is required to configure Client Premium.");
            var price = string.Equals(template.Currency, "VND", StringComparison.OrdinalIgnoreCase)
                ? TokenWalletRules.ToTokensCeiling(template.Price) : template.Price;
            var yearly = command.Request.PlanId == ClientSubscriptionPlanDefaults.YearlyPlanId;
            plan = new SubscriptionPlan
            {
                SubscriptionPlansId = command.Request.PlanId,
                Name = yearly ? "Client Premium Yearly" : "Client Premium Monthly",
                Description = yearly ? "A full year of Premium hiring tools with two months free" : "Premium hiring tools for one month",
                Price = yearly ? price * 10 : price,
                Currency = "GigCoin",
                DurationInDays = yearly ? 365 : template.DurationInDays,
                Features = template.Features,
                TargetRole = (int)UserRole.Client,
                IsActive = true,
                SortOrder = yearly ? (template.SortOrder ?? 0) + 1 : template.SortOrder,
                CreatedAt = clock.UtcNow
            };
            context.Set<SubscriptionPlan>().Add(plan);
        }
        if (plan is null) throw new NotFoundException("Client subscription plan does not exist.");

        var isGigCoin = string.Equals(plan.Currency, "GigCoin", StringComparison.OrdinalIgnoreCase);
        var isVnd = string.Equals(plan.Currency, "VND", StringComparison.OrdinalIgnoreCase);
        if (!isGigCoin && !isVnd) throw new BadRequestException("This plan uses an unsupported purchase currency.");
        var priceInTokens = isVnd ? TokenWalletRules.ToTokensCeiling(plan.Price) : plan.Price;

        var walletTransaction = await ledger.DebitAsync(command.UserId, priceInTokens,
            WalletTransactionType.SubscriptionPurchase, command.Request.IdempotencyKey,
            JsonSerializer.Serialize(new { planId = plan.SubscriptionPlansId, priceInTokens }), cancellationToken);
        var now = clock.UtcNow;
        var compatible = context.Set<Subscription>().AsNoTracking()
            .Where(item => item.UserId == command.UserId && item.Status == SubscriptionStatus.Active &&
                item.EndDate > now)
            .CompatibleWithRole(UserRole.Client);
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
            SubscriptionsId = Guid.NewGuid(), UserId = command.UserId,
            SubscriptionPlansId = plan.SubscriptionPlansId, SubscriptionPlans = plan,
            Status = SubscriptionStatus.Active, StartDate = startsAt,
            EndDate = startsAt.AddDays(plan.DurationInDays), AutoRenew = false,
            PaymentReference = walletTransaction.WalletTransactionsId.ToString(), CreatedAt = now
        };
        context.Set<Subscription>().Add(subscription);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await cache.RemoveAsync($"premium:access:client:{command.UserId:N}", cancellationToken);
        await notifications.CreateNotificationAsync(command.UserId, NotificationType.SubscriptionActivated,
            $"Client Premium activated through {subscription.EndDate:yyyy-MM-dd}", null,
            subscription.SubscriptionsId, nameof(Subscription), cancellationToken);
        return SubscriptionDto.FromEntity(subscription, now);
    }
}
