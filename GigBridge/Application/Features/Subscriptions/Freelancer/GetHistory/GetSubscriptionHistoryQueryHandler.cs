using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Freelancer.GetHistory;

public sealed class GetSubscriptionHistoryQueryHandler(
    IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetSubscriptionHistoryQuery, IReadOnlyList<SubscriptionDto>>
{
    public async Task<IReadOnlyList<SubscriptionDto>> Handle(
        GetSubscriptionHistoryQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subscriptions = await context.Set<Subscription>()
            .AsNoTracking()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == request.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(item => SubscriptionDto.FromEntity(item, now)).ToList();
    }
}
