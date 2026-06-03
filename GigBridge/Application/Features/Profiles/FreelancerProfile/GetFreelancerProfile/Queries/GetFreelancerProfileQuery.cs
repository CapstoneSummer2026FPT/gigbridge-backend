using System;
using Application.Features.Profiles.FreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;

public record GetFreelancerProfileQuery(Guid UserId) : IRequest<FreelancerProfileDetailDto>;
