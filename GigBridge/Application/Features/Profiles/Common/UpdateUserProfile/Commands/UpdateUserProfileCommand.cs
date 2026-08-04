using Application.Features.Profiles.Common.DTOs;
using Application.Features.Profiles.Common.UpdateUserProfile.DTOs;
using MediatR;

namespace Application.Features.Profiles.Common.UpdateUserProfile.Commands;

public sealed record UpdateUserProfileCommand(UpdateUserProfileDto Dto)
    : IRequest<UserProfileDto>;
