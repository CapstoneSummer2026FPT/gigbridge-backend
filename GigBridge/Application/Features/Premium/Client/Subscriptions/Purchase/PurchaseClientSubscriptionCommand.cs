using Application.Features.Subscriptions.Freelancer.DTOs;
using Application.Features.Subscriptions.Freelancer.Purchase;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.Purchase;

public sealed record PurchaseClientSubscriptionCommand(Guid UserId, PurchaseSubscriptionRequest Request)
    : IRequest<SubscriptionDto>;
