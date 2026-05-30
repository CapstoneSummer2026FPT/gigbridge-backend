using Application.Features.Profile.DTOs;
using MediatR;

namespace Application.Features.Profile.Commands
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
