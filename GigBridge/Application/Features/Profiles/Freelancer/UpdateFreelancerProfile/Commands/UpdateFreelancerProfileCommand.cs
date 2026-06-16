using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;

public record UpdateFreelancerProfileCommand(UpdateFreelancerProfileDto Dto)
    : IRequest<FreelancerProfileResponseDto>;
