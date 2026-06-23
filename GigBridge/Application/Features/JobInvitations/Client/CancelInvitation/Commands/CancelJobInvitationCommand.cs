using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Client.CancelInvitation.Commands;

public sealed record CancelJobInvitationCommand(
    Guid UserId,
    Guid InvitationId) : IRequest<JobInvitationDto>;
