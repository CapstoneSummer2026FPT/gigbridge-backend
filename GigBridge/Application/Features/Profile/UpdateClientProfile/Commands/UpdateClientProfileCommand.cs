using Application.Features.Profile.UpdateClientProfile.DTOs;
using MediatR;

namespace Application.Features.Profile.UpdateClientProfile.Commands
{
    public class UpdateClientProfileCommand : IRequest<ClientProfileResponseDto>
    {
        public UpdateClientProfileDto Data { get; set; } = null!;

        public UpdateClientProfileCommand(UpdateClientProfileDto data)
        {
            Data = data;
        }
    }
}
