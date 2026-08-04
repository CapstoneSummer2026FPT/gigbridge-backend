using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.GetCurrent;

public sealed class GetCurrentPromotionQueryHandler(
    IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetCurrentPromotionQuery, PromotionDto?>
{
    public async Task<PromotionDto?> Handle(GetCurrentPromotionQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var promotion = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(item => item.FreelancerProfile.UserId == request.UserId &&
                           item.Status == PromotionStatus.Active &&
                           item.StartTime <= now && item.EndTime > now)
            .FirstOrDefaultAsync(cancellationToken);

        return promotion is null ? null : PromotionDto.FromEntity(promotion);
    }
}
