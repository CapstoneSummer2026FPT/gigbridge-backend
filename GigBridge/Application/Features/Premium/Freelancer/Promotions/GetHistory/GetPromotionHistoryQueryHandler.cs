using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.GetHistory;

public sealed class GetPromotionHistoryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPromotionHistoryQuery, IReadOnlyList<PromotionDto>>
{
    public async Task<IReadOnlyList<PromotionDto>> Handle(
        GetPromotionHistoryQuery request, CancellationToken cancellationToken)
    {
        var promotions = await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(item => item.FreelancerProfile.UserId == request.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return promotions.Select(item => PromotionDto.FromEntity(item)).ToList();
    }
}
