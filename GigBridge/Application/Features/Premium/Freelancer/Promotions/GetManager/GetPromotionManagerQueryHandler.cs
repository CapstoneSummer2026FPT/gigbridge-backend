using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.GetManager;
public sealed class GetPromotionManagerQueryHandler(
    IApplicationDbContext context,
    IDateTimeService clock)
    : IRequestHandler<GetPromotionManagerQuery, PromotionManagerDto>
{
    public async Task<PromotionManagerDto> Handle(GetPromotionManagerQuery request, CancellationToken ct)
    {
        var campaigns = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(x => x.FreelancerProfile.UserId == request.UserId)
            .OrderBy(x => x.StartTime).ToListAsync(ct);
        var active = campaigns.FirstOrDefault(x => x.Status == PromotionStatus.Active);
        var queued = campaigns.Where(x => x.Status == PromotionStatus.Pending)
            .Select(PromotionDto.FromEntity).ToList();
        var history = campaigns.Where(x => x.Status is PromotionStatus.Expired or PromotionStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt).Select(x => PromotionDto.FromEntity(x)).ToList();
        var balance = await context.Set<UserWallet>().AsNoTracking()
            .Where(x => x.UserId == request.UserId).Select(x => x.AvailableTokens)
            .FirstOrDefaultAsync(ct);
        var now = clock.UtcNow;
        var queue = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(x => x.Status == PromotionStatus.Active &&
                        x.StartTime <= now &&
                        x.EndTime > now &&
                        x.QueuePosition > 0)
            .OrderBy(x => x.QueuePosition)
            .Select(x => new PromotionQueueEntryDto(
                x.QueuePosition,
                x.BoostWeight,
                x.FreelancerProfile.UserId == request.UserId))
            .ToListAsync(ct);
        return new PromotionManagerDto(active is null ? null : PromotionDto.FromEntity(active),
            queued, history, await PromotionPolicy.LoadAsync(context, ct), balance, queue);
    }
}
