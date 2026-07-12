using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Subscriptions.Freelancer.GetPlans;

public sealed record GetSubscriptionPlansQuery :
    IRequest<IReadOnlyList<SubscriptionPlanDto>>;
