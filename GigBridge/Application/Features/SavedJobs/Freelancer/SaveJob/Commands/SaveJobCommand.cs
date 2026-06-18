using MediatR;

namespace Application.Features.SavedJobs.Freelancer.SaveJob.Commands;

public record SaveJobCommand(
    Guid UserId,
    Guid JobPostId
) : IRequest<Guid>;