using System;
using Application.Features.Profiles.ClientProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.ClientProfile.GetClientProfile.Queries;

public record GetClientProfileQuery(Guid UserId) : IRequest<ClientProfileDetailDto>;
