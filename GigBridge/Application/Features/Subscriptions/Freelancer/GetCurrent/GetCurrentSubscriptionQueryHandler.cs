using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Freelancer.GetCurrent;

public sealed class GetCurrentSubscriptionQueryHandler : IRequestHandler<GetCurrentSubscriptionQuery, SubscriptionDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public GetCurrentSubscriptionQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<SubscriptionDto?> Handle(GetCurrentSubscriptionQuery query, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var subscription = await _context.Set<Subscription>().AsNoTracking()
            .Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == query.UserId)
            .EffectiveAt(UserRole.Freelancer, now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(ct);

        return subscription is null ? null : SubscriptionDto.FromEntity(subscription, now);
    }
}
