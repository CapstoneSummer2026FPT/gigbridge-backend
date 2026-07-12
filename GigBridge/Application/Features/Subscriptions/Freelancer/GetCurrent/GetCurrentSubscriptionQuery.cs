using Application.Features.Subscriptions.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Subscriptions.Freelancer.GetCurrent;

public sealed record GetCurrentSubscriptionQuery(Guid UserId) : IRequest<SubscriptionDto?>;
