using MediatR;

namespace Application.Features.SavedFreelancers.Client.UnsaveFreelancer.Commands;

public record UnsaveFreelancerCommand(
    Guid UserId,
    Guid FreelancerProfileId
) : IRequest<Unit>;