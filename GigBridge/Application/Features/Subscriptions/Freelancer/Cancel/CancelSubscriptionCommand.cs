using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Subscriptions.Freelancer.Cancel;

public sealed record CancelSubscriptionCommand(Guid UserId) : IRequest<SubscriptionDto>;
