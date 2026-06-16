using Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.ClientProfile.UpdateClientProfile.Commands;

public record UpdateClientProfileCommand(UpdateClientProfileDto Dto)
    : IRequest<ClientProfileResponseDto>;
