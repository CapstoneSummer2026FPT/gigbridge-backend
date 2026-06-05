using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;

public record CreateFreelancerProfileCommand(CreateFreelancerProfileDto Dto) 
    : IRequest<FreelancerProfileResponseDto>, IRequireAuthentication;
