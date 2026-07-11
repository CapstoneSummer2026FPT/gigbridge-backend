using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.GetManager;
public sealed class GetPromotionManagerQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPromotionManagerQuery, PromotionManagerDto>
{
    public async Task<PromotionManagerDto> Handle(GetPromotionManagerQuery request, CancellationToken ct)
    {
        var campaigns = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(x => x.FreelancerProfile.UserId == request.UserId)
            .OrderBy(x => x.StartTime).ToListAsync(ct);
        var active = campaigns.FirstOrDefault(x => x.Status == PromotionStatus.Active);
        var queued = campaigns.Where(x => x.Status == PromotionStatus.Pending)
            .Select((x, index) => PromotionDto.FromEntity(x, index + 1)).ToList();
        var history = campaigns.Where(x => x.Status is PromotionStatus.Expired or PromotionStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt).Select(x => PromotionDto.FromEntity(x)).ToList();
        var balance = await context.Set<UserWallet>().AsNoTracking()
            .Where(x => x.UserId == request.UserId).Select(x => x.AvailableTokens)
            .FirstOrDefaultAsync(ct);
        return new PromotionManagerDto(active is null ? null : PromotionDto.FromEntity(active),
            queued, history, await PromotionPolicy.LoadAsync(context, ct), balance);
    }
}
