using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Subscriptions.Freelancer.Purchase;

public sealed record PurchaseSubscriptionRequest(Guid PlanId, string IdempotencyKey);
public sealed record PurchaseSubscriptionCommand(Guid UserId, PurchaseSubscriptionRequest Request)
    : IRequest<SubscriptionDto>;
