using Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.DTOs;
using MediatR;

namespace Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.Queries;

public record GetMySavedFreelancersQuery(
    Guid UserId,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<IEnumerable<SavedFreelancerDto>>;