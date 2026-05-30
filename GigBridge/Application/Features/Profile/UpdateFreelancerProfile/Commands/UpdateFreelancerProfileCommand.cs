using Application.Features.Profile.UpdateFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Profile.UpdateFreelancerProfile.Commands
{
    public class UpdateFreelancerProfileCommand : IRequest<FreelancerProfileResponseDto>
    {
        public UpdateFreelancerProfileDto Data { get; set; } = null!;

        public UpdateFreelancerProfileCommand(UpdateFreelancerProfileDto data)
        {
            Data = data;
        }
    }
}
