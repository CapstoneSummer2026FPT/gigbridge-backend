using System.Collections.Generic;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetAllFreelancers.Queries;

public record GetAllFreelancersQuery(
    List<string>? Skills = null,
    string? AvailabilityStatus = null,
    double? MinRating = null
) : IRequest<IEnumerable<FreelancerProfileDetailDto>>;
