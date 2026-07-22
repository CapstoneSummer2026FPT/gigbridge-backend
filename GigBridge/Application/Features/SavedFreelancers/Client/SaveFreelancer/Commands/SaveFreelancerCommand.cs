using MediatR;

namespace Application.Features.SavedFreelancers.Client.SaveFreelancer.Commands;

public record SaveFreelancerCommand(
    Guid UserId,
    Guid FreelancerProfileId,
    Guid? MatchRunId = null
) : IRequest<Guid>;
