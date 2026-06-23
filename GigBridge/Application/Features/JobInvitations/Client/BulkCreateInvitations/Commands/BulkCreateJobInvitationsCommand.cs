using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Client.BulkCreateInvitations.Commands;

public sealed record BulkCreateJobInvitationsCommand(
    Guid UserId,
    BulkCreateJobInvitationsRequest Request) : IRequest<BulkJobInvitationResultDto>;
