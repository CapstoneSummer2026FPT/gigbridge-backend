using Application.Features.Profile.DTOs;
using MediatR;

namespace Application.Features.Profile.Commands
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
