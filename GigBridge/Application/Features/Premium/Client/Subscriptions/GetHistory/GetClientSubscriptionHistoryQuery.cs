using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.GetHistory;

public sealed record GetClientSubscriptionHistoryQuery(Guid UserId)
    : IRequest<IReadOnlyList<SubscriptionDto>>;
