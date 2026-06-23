using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Client.CreateInvitation.Commands;

public sealed record CreateJobInvitationCommand(
    Guid UserId,
    CreateJobInvitationRequest Request) : IRequest<JobInvitationDto>;
