using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.AutoRenew.Commands;

public sealed record UpdateSubscriptionAutoRenewCommand(Guid UserId, bool AutoRenew) : IRequest<SubscriptionDto>;
