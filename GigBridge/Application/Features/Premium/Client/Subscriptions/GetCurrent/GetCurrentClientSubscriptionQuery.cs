using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.Subscriptions.GetCurrent;

public sealed record GetCurrentClientSubscriptionQuery(Guid UserId) : IRequest<SubscriptionDto?>;
