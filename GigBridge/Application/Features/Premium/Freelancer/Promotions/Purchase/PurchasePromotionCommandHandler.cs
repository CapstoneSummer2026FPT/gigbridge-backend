using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Premium.Common.Interfaces;
using Application.Features.Wallets.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums.Notifications;
using Domain.Enums.Premium;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.Purchase;

public sealed class PurchasePromotionCommandHandler(
    IApplicationDbContext context, IPremiumAccessService premium,
    IWalletLedgerService ledger, IDateTimeService clock,
    ICacheService cache, INotificationService notifications)
    : IRequestHandler<PurchasePromotionCommand, PromotionDto>
{
    public async Task<PromotionDto> Handle(PurchasePromotionCommand command, CancellationToken cancellationToken)
    {
        await premium.RequirePremiumFreelancerAsync(command.UserId, cancellationToken);

        var existing = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.FreelancerProfile.UserId == command.UserId &&
                item.PurchaseIdempotencyKey == command.Request.IdempotencyKey, cancellationToken);
        if (existing is not null) return PromotionDto.FromEntity(existing);

        var policy = await PromotionPolicy.LoadAsync(context, cancellationToken);
        ValidateCard(command.Request, policy);
        var tokenAmount = command.Request.TokenAmount;
        if (tokenAmount != decimal.Truncate(tokenAmount) || tokenAmount < 0 ||
            tokenAmount > policy.MaximumBoostCoinsPerTransaction)
            throw new BadRequestException($"Promotion amount must be a whole-coin value between 0 and {policy.MaximumBoostCoinsPerTransaction}.");
        var profileId = await context.Set<FreelancerProfile>()
            .Where(item => item.UserId == command.UserId)
            .Select(item => (Guid?)item.FreelancerProfilesId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Freelancer profile does not exist.");

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            PromotionPolicy.QueueTransactionLockKey, cancellationToken);
        var queue = await context.Set<FreelancerProfilePromotion>()
            .Where(item => item.FreelancerProfileId == profileId &&
                (item.Status == PromotionStatus.Active || item.Status == PromotionStatus.Pending))
            .OrderBy(item => item.EndTime).ToListAsync(cancellationToken);
        if (queue.Count(item => item.Status == PromotionStatus.Pending) >= policy.MaxQueuedCampaigns)
            throw new ConflictException("The promotion queue is full.");

        var now = clock.UtcNow;
        var startsAt = queue.Count == 0 ? now : queue[^1].EndTime;
        if (startsAt < now) startsAt = now;
        WalletTransaction? walletTransaction = null;
        if (tokenAmount > 0)
            walletTransaction = await ledger.DebitAsync(
                command.UserId, tokenAmount, WalletTransactionType.PromotionPurchase,
                command.Request.IdempotencyKey,
                JsonSerializer.Serialize(new { campaign = PromotionPolicy.CustomCampaignId }), cancellationToken);
        var promotion = new FreelancerProfilePromotion
        {
            FreelancerProfilePromotionsId = Guid.NewGuid(),
            FreelancerProfileId = profileId,
            PackageId = PromotionPolicy.CustomCampaignId,
            PackageName = PromotionPolicy.CustomCampaignName,
            PurchaseIdempotencyKey = command.Request.IdempotencyKey,
            DurationDays = policy.DefaultDurationDays,
            TokenCost = tokenAmount,
            BoostWeight = PromotionPolicy.BoostWeight(tokenAmount, policy),
            TargetClickCount = PromotionPolicy.TargetClicks(tokenAmount, policy),
            PhotoUrl = command.Request.PhotoUrl.Trim(),
            DisplayName = command.Request.DisplayName.Trim(),
            Quote = string.IsNullOrWhiteSpace(command.Request.Quote) ? null : command.Request.Quote.Trim(),
            ShowQuote = command.Request.ShowQuote,
            JobTitle = string.IsNullOrWhiteSpace(command.Request.JobTitle) ? null : command.Request.JobTitle.Trim(),
            ShowJobTitle = command.Request.ShowJobTitle,
            StartTime = startsAt,
            EndTime = startsAt.AddDays(policy.DefaultDurationDays),
            Status = queue.Count == 0 ? PromotionStatus.Active : PromotionStatus.Pending,
            WalletTransactionId = walletTransaction?.WalletTransactionsId,
            CreatedAt = now,
            ActivatedAt = queue.Count == 0 ? now : null
        };
        context.Set<FreelancerProfilePromotion>().Add(promotion);
        await context.SaveChangesAsync(cancellationToken);
        await PromotionPolicy.RecalculateQueuePositionsAsync(
            context, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await cache.RemoveAsync(PromotionPolicy.UserCacheKey(command.UserId), cancellationToken);
        await cache.RemoveAsync(PromotionPolicy.FeedCacheKey, cancellationToken);
        if (promotion.Status == PromotionStatus.Active)
            await notifications.CreateNotificationAsync(command.UserId,
                NotificationType.PromotionActivated, "Promotion activated",
                $"Your profile is promoted until {promotion.EndTime:O}.", promotion.FreelancerProfilePromotionsId,
                nameof(FreelancerProfilePromotion), cancellationToken);

        return PromotionDto.FromEntity(promotion);
    }

    private static void ValidateCard(PurchasePromotionRequest request, PromotionPolicyDto policy)
    {
        if (request.PhotoUrl.Trim().Length > policy.PhotoUrlMaxLength)
            throw new BadRequestException("Promotion photo URL is too long.");
        if (request.DisplayName.Trim().Length > policy.DisplayNameMaxLength)
            throw new BadRequestException("Promotion display name is too long.");
        if (request.ShowQuote && string.IsNullOrWhiteSpace(request.Quote))
            throw new BadRequestException("A quote is required when quote display is enabled.");
        if (request.Quote?.Trim().Length > policy.QuoteMaxLength)
            throw new BadRequestException("Promotion quote is too long.");
        if (request.ShowJobTitle && string.IsNullOrWhiteSpace(request.JobTitle))
            throw new BadRequestException("A job title is required when job title display is enabled.");
        if (request.JobTitle?.Trim().Length > policy.JobTitleMaxLength)
            throw new BadRequestException("Promotion job title is too long.");
    }
}
