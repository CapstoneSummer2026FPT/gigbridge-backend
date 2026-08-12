using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Premium.Common;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPostPromotionEntity = Domain.Entities.JobPostPromotion;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class TrackJobPromotionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock,
    ICacheService cache) : IRequestHandler<TrackJobPromotionCommand, JobPromotionInteractionDto>
{
    private const int DeduplicationSeconds = 60;

    public async Task<JobPromotionInteractionDto> Handle(
        TrackJobPromotionCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var identity = PromotionInteractionIdentityFactory.Create(
            "job", request.PromotionId, request.Type.ToString(), request.VisitorKey, now, DeduplicationSeconds);
        try
        {
            if (await cache.GetAsync<bool?>(identity.Key, cancellationToken) == true)
                return await LoadAsync(request.PromotionId, cancellationToken);
        }
        catch
        {
            // The database idempotency record remains the source of truth when cache is unavailable.
        }

        await using (var transaction = await context.BeginTransactionAsync(cancellationToken))
        {
            await transaction.AcquireTransactionLockAsync(identity.LockKey, cancellationToken);
            var alreadyRecorded = await context.Set<PremiumUsageEvent>().AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == identity.Key, cancellationToken);
            if (!alreadyRecorded)
            {
                var query = context.Set<JobPostPromotionEntity>().Where(x =>
                    x.JobPostPromotionsId == request.PromotionId &&
                    x.FeaturedFrom <= now && x.FeaturedUntil > now);
                var affected = request.Type == JobPromotionInteractionType.Impression
                    ? await query.ExecuteUpdateAsync(update => update.SetProperty(
                        x => x.ImpressionCount, x => x.ImpressionCount + 1), cancellationToken)
                    : await query.ExecuteUpdateAsync(update => update.SetProperty(
                        x => x.ClickCount, x => x.ClickCount + 1), cancellationToken);
                if (affected == 0) throw new NotFoundException("Active job promotion does not exist.");
                context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
                {
                    PremiumUsageEventId = Guid.NewGuid(),
                    Type = request.Type == JobPromotionInteractionType.Impression
                        ? Domain.Enums.Premium.PremiumUsageEventType.PromotionImpression
                        : Domain.Enums.Premium.PremiumUsageEventType.PromotionClick,
                    PromotionId = request.PromotionId,
                    IdempotencyKey = identity.Key,
                    OccurredAt = now
                });
                await context.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await cache.SetAsync(identity.Key, true, TimeSpan.FromSeconds(DeduplicationSeconds), cancellationToken);
        }
        catch
        {
            // Cache failure must not turn a successfully committed interaction into a 500 response.
        }
        return await LoadAsync(request.PromotionId, cancellationToken);
    }

    private Task<JobPromotionInteractionDto> LoadAsync(Guid promotionId, CancellationToken cancellationToken) =>
        context.Set<JobPostPromotionEntity>().AsNoTracking()
            .Where(x => x.JobPostPromotionsId == promotionId)
            .Select(x => new JobPromotionInteractionDto(
                x.JobPostPromotionsId, x.ImpressionCount, x.ClickCount))
            .SingleAsync(cancellationToken);

}
