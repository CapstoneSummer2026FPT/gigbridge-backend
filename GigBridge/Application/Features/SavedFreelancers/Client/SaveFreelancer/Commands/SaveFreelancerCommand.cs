using MediatR;

namespace Application.Features.SavedFreelancers.Client.SaveFreelancer.Commands;

public record SaveFreelancerCommand(
    Guid UserId,
    Guid FreelancerProfileId
) : IRequest<Guid>;