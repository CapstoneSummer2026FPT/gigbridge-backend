using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.Cancel;

public sealed class CancelClientSubscriptionCommandHandler(
    IApplicationDbContext context, IDateTimeService clock,
    ICacheService cache, INotificationService notifications)
    : IRequestHandler<CancelClientSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(
        CancelClientSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscription = await context.Set<Subscription>()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == command.UserId)
            .EffectiveAt(UserRole.Client, now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Active client subscription does not exist.");

        if (subscription.CancelledAt is null || subscription.AutoRenew == true)
        {
            subscription.CancelledAt ??= now;
            subscription.AutoRenew = false;
            subscription.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync($"premium:access:client:{command.UserId:N}", cancellationToken);
            await notifications.CreateNotificationAsync(
                command.UserId, NotificationType.SubscriptionCancelled,
                "Client Premium renewal cancelled",
                $"Your Client Premium benefits remain active until {subscription.EndDate:O}.",
                subscription.SubscriptionsId, nameof(Subscription), cancellationToken);
        }

        return SubscriptionDto.FromEntity(subscription, now);
    }
}
