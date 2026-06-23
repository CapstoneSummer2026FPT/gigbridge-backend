using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Freelancer.GetMyInvitations.Queries;

public sealed record GetMyJobInvitationsQuery(
    Guid UserId,
    int? Status,
    int Page,
    int PageSize) : IRequest<IEnumerable<JobInvitationDto>>;
