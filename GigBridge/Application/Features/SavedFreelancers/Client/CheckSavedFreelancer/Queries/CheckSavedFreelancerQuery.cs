using MediatR;

namespace Application.Features.SavedFreelancers.Client.CheckSavedFreelancer.Queries;

public record CheckSavedFreelancerQuery(
    Guid UserId,
    Guid FreelancerProfileId
) : IRequest<bool>;