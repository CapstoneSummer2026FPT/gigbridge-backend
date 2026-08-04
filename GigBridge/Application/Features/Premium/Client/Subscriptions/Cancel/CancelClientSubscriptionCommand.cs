using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.Cancel;

public sealed record CancelClientSubscriptionCommand(Guid UserId) : IRequest<SubscriptionDto>;
