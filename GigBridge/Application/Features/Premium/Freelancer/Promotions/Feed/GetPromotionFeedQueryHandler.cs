using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace Application.Features.Premium.Freelancer.Promotions.Feed;
public sealed class GetPromotionFeedQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetPromotionFeedQuery, IReadOnlyList<PublicPromotionCardDto>>
{
    public async Task<IReadOnlyList<PublicPromotionCardDto>> Handle(GetPromotionFeedQuery request, CancellationToken ct)
    {
        var policy = await PromotionPolicy.LoadAsync(context, ct);
        var requestedLimit = request.Limit <= 0 ? policy.DefaultFeedLimit : request.Limit;
        var limit = Math.Clamp(requestedLimit, 1, policy.MaximumFeedLimit);
        var now = clock.UtcNow;
        return await context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(x => x.Status == PromotionStatus.Active && x.StartTime <= now && x.EndTime > now)
            .OrderByDescending(x => x.BoostWeight).ThenBy(x => x.ImpressionCount)
            .Take(limit).Select(x => new PublicPromotionCardDto(x.FreelancerProfilePromotionsId,
                x.FreelancerProfile.UserId, x.PhotoUrl, x.DisplayName, x.Quote,
                x.ShowQuote, x.JobTitle, x.ShowJobTitle)).ToListAsync(ct);
    }
}
