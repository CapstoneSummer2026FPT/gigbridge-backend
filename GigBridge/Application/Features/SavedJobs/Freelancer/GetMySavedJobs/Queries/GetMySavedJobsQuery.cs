using Application.Features.SavedJobs.Freelancer.GetMySavedJobs.DTOs;
using MediatR;

namespace Application.Features.SavedJobs.Freelancer.GetMySavedJobs.Queries;

public record GetMySavedJobsQuery(
    Guid UserId,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<IEnumerable<SavedJobDto>>;