using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.Boost;
public sealed class BoostPromotionCommandHandler(IApplicationDbContext context, IPremiumAccessService premium,
    IWalletLedgerService ledger, ICacheService cache, IDateTimeService clock) : IRequestHandler<BoostPromotionCommand, PromotionDto>
{
    public async Task<PromotionDto> Handle(BoostPromotionCommand command, CancellationToken ct)
    {
        await premium.RequirePremiumFreelancerAsync(command.UserId, ct);
        var policy = await PromotionPolicy.LoadAsync(context, ct);
        var amount = command.Request.TokenAmount;
        if (amount != decimal.Truncate(amount) || amount < policy.MinimumBoostCoins || amount > policy.MaximumBoostCoinsPerTransaction)
            throw new BadRequestException($"Boost must be a whole-coin amount between {policy.MinimumBoostCoins} and {policy.MaximumBoostCoinsPerTransaction}.");
        var promotion = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.FreelancerProfilePromotionsId == command.PromotionId && x.FreelancerProfile.UserId == command.UserId, ct)
            ?? throw new NotFoundException("Promotion does not exist.");
        if (promotion.Status is not (PromotionStatus.Active or PromotionStatus.Pending))
            throw new ConflictException("Only active or queued promotions can be boosted.");
        var metadata = JsonSerializer.Serialize(new { promotionId = promotion.FreelancerProfilePromotionsId, boostTokenAmount = amount });
        var existing = await context.Set<WalletTransaction>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == command.UserId && x.IdempotencyKey == command.Request.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.Type != (int)WalletTransactionType.PromotionPurchase || existing.TokenAmount != amount || existing.Metadata != metadata)
                throw new ConflictException("The idempotency key was already used for a different wallet operation.");
            return PromotionDto.FromEntity(promotion);
        }
        await using var transaction = await context.BeginTransactionAsync(ct);
        await ledger.DebitAsync(command.UserId, amount, WalletTransactionType.PromotionPurchase,
            command.Request.IdempotencyKey, metadata, ct);
        var targetIncrement = decimal.ToInt32(amount * policy.TargetClicksPerCoin);
        var affected = await context.Set<FreelancerProfilePromotion>()
            .Where(x => x.FreelancerProfilePromotionsId == command.PromotionId &&
                x.FreelancerProfile.UserId == command.UserId &&
                (x.Status == PromotionStatus.Active || x.Status == PromotionStatus.Pending))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.TokenCost, x => x.TokenCost + amount)
                .SetProperty(x => x.BoostWeight, x => x.BoostWeight + amount * policy.BoostWeightPerCoin)
                .SetProperty(x => x.TargetClickCount, x => x.TargetClickCount + targetIncrement), ct);
        if (affected != 1) throw new ConflictException("The promotion changed concurrently. Retry with the same idempotency key.");
        await PromotionPolicy.RecalculateQueuePositionsAsync(context, clock.UtcNow, ct);
        await transaction.CommitAsync(ct);
        await cache.RemoveAsync(PromotionPolicy.UserCacheKey(command.UserId), ct);
        await cache.RemoveAsync(PromotionPolicy.FeedCacheKey, ct);
        var updated = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .SingleAsync(x => x.FreelancerProfilePromotionsId == command.PromotionId, ct);
        return PromotionDto.FromEntity(updated);
    }
}
