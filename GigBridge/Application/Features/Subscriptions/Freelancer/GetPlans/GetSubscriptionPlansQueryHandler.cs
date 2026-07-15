using Application.Common.Interfaces;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Freelancer.GetPlans;

public sealed class GetSubscriptionPlansQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await context.Set<SubscriptionPlan>().AsNoTracking()
            .Where(plan => plan.IsActive == true &&
                (plan.TargetRole == null || plan.TargetRole == (int)UserRole.Freelancer))
            .OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Price)
            .ToListAsync(cancellationToken);

        var result = plans.Select(SubscriptionPlanDto.FromEntity).ToList();
        if (!result.Any(plan => plan.Price > 0 && plan.DurationInDays >= 360))
        {
            var monthly = result.FirstOrDefault(plan => plan.Price > 0 && plan.DurationInDays < 360);
            if (monthly is not null)
                result.Add(new SubscriptionPlanDto(
                    PremiumPlanDefaults.YearlyPromotionPlanId,
                    "Freelancer Premium Yearly",
                    "A full year of Freelancer Premium with two months free",
                    monthly.Price * 10, "GigCoin", 365, monthly.Features,
                    (monthly.SortOrder ?? 0) + 1));
        }

        return result.OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Price).ToList();
    }
}
