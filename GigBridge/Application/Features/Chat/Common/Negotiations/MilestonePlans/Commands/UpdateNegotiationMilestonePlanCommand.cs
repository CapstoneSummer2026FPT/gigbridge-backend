using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Negotiations.MilestonePlans.Commands;

public sealed record UpdateNegotiationMilestonePlanCommand(
    Guid ConversationId,
    Guid UserId,
    UpdateNegotiationMilestonePlanRequest Request) : IRequest<IReadOnlyCollection<NegotiationMilestoneDto>>;
