using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.GetHistory;

public sealed class GetClientSubscriptionHistoryQueryHandler(
    IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetClientSubscriptionHistoryQuery, IReadOnlyList<SubscriptionDto>>
{
    public async Task<IReadOnlyList<SubscriptionDto>> Handle(
        GetClientSubscriptionHistoryQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscriptions = await context.Set<Subscription>()
            .AsNoTracking()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == query.UserId &&
                item.SubscriptionPlans.Price > 0 &&
                (item.SubscriptionPlans.TargetRole == null ||
                 item.SubscriptionPlans.TargetRole == (int)UserRole.Client))
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(item => SubscriptionDto.FromEntity(item, now)).ToList();
    }
}
