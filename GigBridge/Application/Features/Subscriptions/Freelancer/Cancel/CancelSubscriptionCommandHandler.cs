using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Freelancer.Cancel;

public sealed class CancelSubscriptionCommandHandler(
    IApplicationDbContext context, IDateTimeService clock,
    ICacheService cache, INotificationService notifications)
    : IRequestHandler<CancelSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(
        CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscription = await context.Set<Subscription>()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == command.UserId &&
                           item.Status == SubscriptionStatus.Active &&
                           item.SubscriptionPlans.Price > 0 &&
                           item.StartDate <= now && item.EndDate > now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Active subscription does not exist.");

        if (subscription.CancelledAt is null)
        {
            subscription.CancelledAt = now;
            subscription.AutoRenew = false;
            await context.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync($"premium:access:{command.UserId:N}", cancellationToken);
            await notifications.CreateNotificationAsync(
                command.UserId, NotificationType.SubscriptionCancelled,
                "Subscription renewal cancelled",
                $"Your Premium benefits remain active until {subscription.EndDate:O}.",
                subscription.SubscriptionsId, nameof(Subscription), cancellationToken);
        }

        return SubscriptionDto.FromEntity(subscription, now);
    }
}
