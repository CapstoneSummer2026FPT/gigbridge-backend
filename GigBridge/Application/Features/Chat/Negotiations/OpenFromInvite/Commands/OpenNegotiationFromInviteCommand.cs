using MediatR;

namespace Application.Features.Chat.Negotiations.OpenFromInvite.Commands;

public record OpenNegotiationFromInviteCommand(
    Guid JobPostId,
    Guid FreelancerProfileId,
    Guid UserId) : IRequest<Guid>;
