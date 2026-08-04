using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Freelancer.ViewInvitation.Commands;

public sealed record ViewJobInvitationCommand(
    Guid UserId,
    Guid InvitationId) : IRequest<JobInvitationDto>;
