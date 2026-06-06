using Application.Common.Interfaces;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.ClientProfile.CreateClientProfile.Commands;

public record CreateClientProfileCommand(CreateClientProfileDto Dto) 
    : IRequest<ClientProfileResponseDto>;
