using MediatR;

namespace Application.Features.SavedJobs.Freelancer.CheckSavedJob.Queries;

public record CheckSavedJobQuery(
    Guid UserId,
    Guid JobPostId
) : IRequest<bool>;