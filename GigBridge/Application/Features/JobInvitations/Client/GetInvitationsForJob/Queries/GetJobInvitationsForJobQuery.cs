using Application.Features.JobInvitations.Common.DTOs;
using MediatR;

namespace Application.Features.JobInvitations.Client.GetInvitationsForJob.Queries;

public sealed record GetJobInvitationsForJobQuery(
    Guid UserId,
    Guid JobPostId) : IRequest<IEnumerable<JobInvitationDto>>;
