using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.GetCurrent;

public sealed class GetCurrentClientSubscriptionQueryHandler(
    IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetCurrentClientSubscriptionQuery, SubscriptionDto?>
{
    public async Task<SubscriptionDto?> Handle(
        GetCurrentClientSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscription = await context.Set<Subscription>().AsNoTracking()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == query.UserId)
            .EffectiveAt(UserRole.Client, now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(cancellationToken);
        return subscription is null ? null : SubscriptionDto.FromEntity(subscription, now);
    }
}
