using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Subscriptions.Freelancer.GetHistory;

public sealed record GetSubscriptionHistoryQuery(Guid UserId) :
    IRequest<IReadOnlyList<SubscriptionDto>>;
