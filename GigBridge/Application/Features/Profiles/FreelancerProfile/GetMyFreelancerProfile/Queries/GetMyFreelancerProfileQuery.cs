using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetMyFreelancerProfile.Queries;

public record GetMyFreelancerProfileQuery() : IRequest<FreelancerProfileDetailDto>, IRequireAuthentication;
