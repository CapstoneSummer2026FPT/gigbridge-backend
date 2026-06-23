using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Freelancer.ApplyInvitation.Commands;

public sealed record ApplyJobInvitationCommand(
    Guid UserId,
    Guid InvitationId) : IRequest<JobInvitationDto>;
