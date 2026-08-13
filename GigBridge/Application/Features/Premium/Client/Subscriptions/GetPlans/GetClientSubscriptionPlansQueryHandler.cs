using Application.Common.Interfaces;
using Application.Features.Premium.Client.Subscriptions.DTOs;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.Subscriptions.GetPlans;

public sealed class GetClientSubscriptionPlansQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClientSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetClientSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var configured = await context.Set<SubscriptionPlan>().AsNoTracking()
            .Where(plan => plan.IsActive == true && plan.Price > 0 &&
                (plan.TargetRole == null || plan.TargetRole == (int)UserRole.Client))
            .OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Price)
            .ToListAsync(cancellationToken);
        var result = configured.Select(SubscriptionPlanDto.FromEntity).ToList();

        var template = configured.FirstOrDefault(plan => plan.DurationInDays < 360)
            ?? await context.Set<SubscriptionPlan>().AsNoTracking()
                .Where(plan => plan.IsActive == true && plan.Price > 0 && plan.DurationInDays < 360)
                .OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Price)
                .FirstOrDefaultAsync(cancellationToken);
        if (template is null) return result;

        var monthly = SubscriptionPlanDto.FromEntity(template);
        if (!result.Any(plan => plan.DurationInDays < 360))
            result.Add(monthly with {
                Id = ClientSubscriptionPlanDefaults.MonthlyPlanId,
                Name = "Client Premium Monthly",
                Description = "Premium hiring tools for one month"
            });
        if (!result.Any(plan => plan.DurationInDays >= 360))
            result.Add(monthly with {
                Id = ClientSubscriptionPlanDefaults.YearlyPlanId,
                Name = "Client Premium Yearly",
                Description = "A full year of Premium hiring tools with two months free",
                Price = monthly.Price * 10,
                DurationInDays = 365,
                SortOrder = (monthly.SortOrder ?? 0) + 1
            });
        return result.OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Price).ToList();
    }
}
