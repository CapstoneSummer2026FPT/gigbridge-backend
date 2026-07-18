using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.ClientProfile.GetMyClientProfile.Queries;

public record GetMyClientProfileQuery : IRequest<ClientProfileDetailDto>;
