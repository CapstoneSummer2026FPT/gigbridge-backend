using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Client.GetMySentInvitations.Queries;

public sealed record GetMySentJobInvitationsQuery(
    Guid UserId,
    int? Status,
    Guid? JobPostId,
    int Page,
    int PageSize) : IRequest<IEnumerable<JobInvitationDto>>;
