using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPostPromotionEntity = Domain.Entities.JobPostPromotion;

namespace Application.Features.Premium.Client.JobPostPromotion.Queries;

public sealed class GetJobPromotionFeedQueryHandler(
    IApplicationDbContext context,
    IDateTimeService clock)
    : IRequestHandler<GetJobPromotionFeedQuery, IReadOnlyList<PublicJobPromotionCardDto>>
{
    public async Task<IReadOnlyList<PublicJobPromotionCardDto>> Handle(
        GetJobPromotionFeedQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 50);
        var now = clock.UtcNow;
        return await context.Set<JobPostPromotionEntity>().AsNoTracking()
            .Where(x => x.FeaturedFrom <= now && x.FeaturedUntil > now &&
                x.JobPost.Status == 1 && x.JobPost.Visibility == 0)
            .OrderBy(x => x.ImpressionCount)
            .ThenByDescending(x => x.FeaturedFrom)
            .Take(limit)
            .Select(x => new PublicJobPromotionCardDto(
                x.JobPostPromotionsId,
                x.JobPostId,
                x.ImageUrl,
                x.PromotionTitle,
                x.PromotionDescription,
                x.FeaturedUntil))
            .ToListAsync(cancellationToken);
    }
}
