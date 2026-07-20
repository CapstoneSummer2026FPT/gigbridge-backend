using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.AutoRenew.Commands;

public sealed record UpdateClientSubscriptionAutoRenewCommand(Guid UserId, bool AutoRenew)
    : IRequest<SubscriptionDto>;
