using System;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;

public record GetFreelancerProfileQuery(Guid UserId) : IRequest<FreelancerProfileDetailDto>;
