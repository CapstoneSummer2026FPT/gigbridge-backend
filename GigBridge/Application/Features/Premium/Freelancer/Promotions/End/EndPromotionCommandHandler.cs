using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.End;

public sealed class EndPromotionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock,
    ICacheService cache,
    INotificationService notifications)
    : IRequestHandler<EndPromotionCommand, PromotionDto>
{
    public async Task<PromotionDto> Handle(
        EndPromotionCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await context.Set<FreelancerProfilePromotion>()
            .AsNoTracking()
            .Where(item => item.FreelancerProfilePromotionsId == command.PromotionId &&
                           item.FreelancerProfile.UserId == command.UserId)
            .Select(item => new
            {
                item.FreelancerProfilePromotionsId,
                item.FreelancerProfileId,
                item.Status
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Promotion does not exist.");

        if (promotion.Status != PromotionStatus.Active)
            throw new ConflictException("Only an active promotion can be ended early.");

        var now = clock.UtcNow;
        FreelancerProfilePromotion? next;
        await using (var transaction = await context.BeginTransactionAsync(cancellationToken))
        {
            var affected = await context.Set<FreelancerProfilePromotion>()
                .Where(item =>
                    item.FreelancerProfilePromotionsId == promotion.FreelancerProfilePromotionsId &&
                    item.Status == PromotionStatus.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, PromotionStatus.Cancelled)
                    .SetProperty(item => item.CancelledAt, now)
                    .SetProperty(item => item.EndTime, now), cancellationToken);
            if (affected != 1)
                throw new ConflictException("The promotion has already ended.");

            var queued = await context.Set<FreelancerProfilePromotion>()
                .Where(item => item.FreelancerProfileId == promotion.FreelancerProfileId &&
                               item.Status == PromotionStatus.Pending)
                .OrderBy(item => item.StartTime)
                .ToListAsync(cancellationToken);
            next = queued.FirstOrDefault();
            if (next is not null)
            {
                next.Status = PromotionStatus.Active;
                next.ActivatedAt = now;
                next.StartTime = now;
                next.EndTime = now.AddDays(next.DurationDays);
                var cursor = next.EndTime;
                foreach (var item in queued.Skip(1))
                {
                    item.StartTime = cursor;
                    item.EndTime = cursor.AddDays(item.DurationDays);
                    cursor = item.EndTime;
                }
                await context.SaveChangesAsync(cancellationToken);
            }

            await PromotionPolicy.RecalculateQueuePositionsAsync(
                context, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await cache.RemoveAsync(PromotionPolicy.UserCacheKey(command.UserId), cancellationToken);
        await cache.RemoveAsync(PromotionPolicy.FeedCacheKey, cancellationToken);
        await notifications.CreateNotificationAsync(
            command.UserId,
            NotificationType.PromotionExpired,
            "Promotion ended",
            "Your profile promotion was ended early.",
            promotion.FreelancerProfilePromotionsId,
            nameof(FreelancerProfilePromotion),
            cancellationToken);

        if (next is not null)
            await notifications.CreateNotificationAsync(
                command.UserId,
                NotificationType.PromotionActivated,
                "Promotion activated",
                $"Your next profile promotion is active until {next.EndTime:O}.",
                next.FreelancerProfilePromotionsId,
                nameof(FreelancerProfilePromotion),
                cancellationToken);

        var ended = await context.Set<FreelancerProfilePromotion>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.FreelancerProfilePromotionsId == promotion.FreelancerProfilePromotionsId,
                cancellationToken);
        return PromotionDto.FromEntity(ended);
    }
}
