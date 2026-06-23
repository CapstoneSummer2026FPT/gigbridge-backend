using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Freelancer.DeclineInvitation.Commands;

public sealed record DeclineJobInvitationCommand(
    Guid UserId,
    Guid InvitationId,
    string? Reason) : IRequest<JobInvitationDto>;
