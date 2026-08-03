using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.AutoRenew.Commands;

public sealed class UpdateSubscriptionAutoRenewCommandHandler : IRequestHandler<UpdateSubscriptionAutoRenewCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly ICacheService _cache;

    public UpdateSubscriptionAutoRenewCommandHandler(IApplicationDbContext context, IDateTimeService clock, ICacheService cache)
    {
        _context = context;
        _clock = clock;
        _cache = cache;
    }

    public async Task<SubscriptionDto> Handle(UpdateSubscriptionAutoRenewCommand command, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var subscription = await _context.Set<Subscription>().Include(item => item.SubscriptionPlans)
            .Where(item => item.UserId == command.UserId)
            .EffectiveAt(UserRole.Freelancer, now)
            .OrderByDescending(item => item.EndDate)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Active subscription does not exist.");

        subscription.AutoRenew = command.AutoRenew;
        subscription.CancelledAt = command.AutoRenew ? null : now;
        subscription.UpdatedAt = now;
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"premium:access:{command.UserId:N}", ct);
        return SubscriptionDto.FromEntity(subscription, now);
    }
}
