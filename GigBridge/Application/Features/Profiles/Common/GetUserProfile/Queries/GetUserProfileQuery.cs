using Application.Features.Profiles.Common.DTOs;
using MediatR;

namespace Application.Features.Profiles.Common.GetUserProfile.Queries;

public sealed record GetUserProfileQuery(Guid UserId) : IRequest<UserProfileDto>;
