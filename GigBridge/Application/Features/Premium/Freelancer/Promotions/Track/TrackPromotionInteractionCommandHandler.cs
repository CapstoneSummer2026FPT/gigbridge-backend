using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Common;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace Application.Features.Premium.Freelancer.Promotions.Track;
public sealed class TrackPromotionInteractionCommandHandler(IApplicationDbContext context,
    ICacheService cache, IDateTimeService clock) : IRequestHandler<TrackPromotionInteractionCommand, PromotionInteractionResultDto>
{
    public async Task<PromotionInteractionResultDto> Handle(TrackPromotionInteractionCommand command, CancellationToken ct)
    {
        var policy = await PromotionPolicy.LoadAsync(context, ct);
        if (string.IsNullOrWhiteSpace(command.VisitorKey) || command.VisitorKey.Length > policy.VisitorKeyMaxLength)
            throw new BadRequestException("A valid visitor key is required.");
        var now = clock.UtcNow;
        var deduplicationSeconds = Math.Max(1, policy.InteractionDeduplicationSeconds);
        var identity = PromotionInteractionIdentityFactory.Create(
            "profile", command.PromotionId, command.Type.ToString(), command.VisitorKey, now, deduplicationSeconds);
        try
        {
            if (await cache.GetAsync<bool?>(identity.Key, ct) == true)
                return ToResult(await LoadAsync(command.PromotionId, ct));
        }
        catch
        {
            // The database idempotency record remains the source of truth when cache is unavailable.
        }

        await using (var transaction = await context.BeginTransactionAsync(ct))
        {
            await transaction.AcquireTransactionLockAsync(identity.LockKey, ct);
            var alreadyRecorded = await context.Set<PremiumUsageEvent>().AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == identity.Key, ct);
            if (!alreadyRecorded)
            {
                var query = context.Set<FreelancerProfilePromotion>().Where(x =>
                    x.FreelancerProfilePromotionsId == command.PromotionId && x.Status == PromotionStatus.Active &&
                    x.StartTime <= now && x.EndTime > now);
                var affected = command.Type == PromotionInteractionType.Impression
                    ? await query.ExecuteUpdateAsync(s => s.SetProperty(x => x.ImpressionCount, x => x.ImpressionCount + 1), ct)
                    : await query.ExecuteUpdateAsync(s => s.SetProperty(x => x.ClickCount, x => x.ClickCount + 1), ct);
                if (affected == 0) throw new NotFoundException("Active promotion does not exist.");
                context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
                {
                    PremiumUsageEventId = Guid.NewGuid(),
                    Type = command.Type == PromotionInteractionType.Impression
                        ? PremiumUsageEventType.PromotionImpression
                        : PremiumUsageEventType.PromotionClick,
                    PromotionId = command.PromotionId,
                    IdempotencyKey = identity.Key,
                    OccurredAt = now
                });
                await context.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }

        if (command.Type == PromotionInteractionType.Click)
            await CompleteAtTargetAsync(command.PromotionId, now, ct);
        try
        {
            await cache.SetAsync(identity.Key, true, TimeSpan.FromSeconds(deduplicationSeconds), ct);
        }
        catch
        {
            // Cache failure must not turn a successfully committed interaction into a 500 response.
        }
        return ToResult(await LoadAsync(command.PromotionId, ct));
    }

    private async Task CompleteAtTargetAsync(Guid id, DateTime now, CancellationToken ct)
    {
        await using var transaction = await context.BeginTransactionAsync(ct);
        await transaction.AcquireTransactionLockAsync(
            PromotionPolicy.QueueTransactionLockKey, ct);
        var completed = await context.Set<FreelancerProfilePromotion>()
            .Where(x => x.FreelancerProfilePromotionsId == id && x.Status == PromotionStatus.Active && x.ClickCount >= x.TargetClickCount)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PromotionStatus.Expired)
                .SetProperty(x => x.ExpiredAt, now).SetProperty(x => x.ClickCount, 0), ct);
        if (completed == 1)
        {
            var profileId = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
                .Where(x => x.FreelancerProfilePromotionsId == id).Select(x => x.FreelancerProfileId).SingleAsync(ct);
            var next = await context.Set<FreelancerProfilePromotion>()
                .Where(x => x.FreelancerProfileId == profileId && x.Status == PromotionStatus.Pending)
                .OrderBy(x => x.StartTime).FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.Status = PromotionStatus.Active; next.ActivatedAt = now;
                next.StartTime = now; next.EndTime = now.AddDays(next.DurationDays);
                var cursor = next.EndTime;
                var remaining = await context.Set<FreelancerProfilePromotion>()
                    .Where(x => x.FreelancerProfileId == profileId && x.Status == PromotionStatus.Pending && x.FreelancerProfilePromotionsId != next.FreelancerProfilePromotionsId)
                    .OrderBy(x => x.StartTime).ToListAsync(ct);
                foreach (var item in remaining) { item.StartTime = cursor; item.EndTime = cursor.AddDays(item.DurationDays); cursor = item.EndTime; }
                await context.SaveChangesAsync(ct);
            }
            await PromotionPolicy.RecalculateQueuePositionsAsync(context, now, ct);
        }
        await transaction.CommitAsync(ct);
        await cache.RemoveAsync(PromotionPolicy.FeedCacheKey, ct);
    }

    private Task<FreelancerProfilePromotion> LoadAsync(Guid id, CancellationToken ct) =>
        context.Set<FreelancerProfilePromotion>().AsNoTracking().SingleAsync(x => x.FreelancerProfilePromotionsId == id, ct);

    private static PromotionInteractionResultDto ToResult(FreelancerProfilePromotion value) =>
        new(value.FreelancerProfilePromotionsId, value.Status, value.ClickCount, value.TargetClickCount);
}
