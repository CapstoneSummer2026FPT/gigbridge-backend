using MediatR;

namespace Application.Features.SavedJobs.Freelancer.UnsaveJob.Commands;

public record UnsaveJobCommand(
    Guid UserId,
    Guid JobPostId
) : IRequest<Unit>;