using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Negotiations.MilestonePlans.Queries;

public sealed record GetNegotiationMilestonePlanQuery(Guid ConversationId, Guid UserId)
    : IRequest<IReadOnlyCollection<NegotiationMilestoneDto>>;
