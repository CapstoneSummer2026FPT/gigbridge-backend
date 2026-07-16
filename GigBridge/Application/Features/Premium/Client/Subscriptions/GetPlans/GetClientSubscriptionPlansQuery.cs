using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.GetPlans;

public sealed record GetClientSubscriptionPlansQuery : IRequest<IReadOnlyList<SubscriptionPlanDto>>;
