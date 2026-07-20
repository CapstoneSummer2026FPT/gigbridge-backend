using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
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
            .Where(item => item.UserId == command.UserId &&
                item.Status == SubscriptionStatus.Active &&
                item.StartDate <= now && item.EndDate > now &&
                item.SubscriptionPlans.IsActive == true &&
                item.SubscriptionPlans.Price > 0 &&
                (item.SubscriptionPlans.TargetRole == null ||
                 item.SubscriptionPlans.TargetRole == (int)UserRole.Client))
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
