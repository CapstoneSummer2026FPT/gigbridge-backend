using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.AutoRenew.Commands;

public sealed class UpdateClientSubscriptionAutoRenewCommandHandler(
    IApplicationDbContext context, IDateTimeService clock, ICacheService cache)
    : IRequestHandler<UpdateClientSubscriptionAutoRenewCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(
        UpdateClientSubscriptionAutoRenewCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscription = await context.Set<Subscription>()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == command.UserId)
            .EffectiveAt(UserRole.Client, now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Active client subscription does not exist.");

        subscription.AutoRenew = command.AutoRenew;
        subscription.CancelledAt = command.AutoRenew ? null : subscription.CancelledAt ?? now;
        subscription.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync($"premium:access:client:{command.UserId:N}", cancellationToken);

        return SubscriptionDto.FromEntity(subscription, now);
    }
}
