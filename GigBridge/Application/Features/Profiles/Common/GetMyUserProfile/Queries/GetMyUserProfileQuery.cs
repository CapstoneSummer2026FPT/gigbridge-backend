using Application.Features.Profiles.Common.DTOs;
using MediatR;

namespace Application.Features.Profiles.Common.GetMyUserProfile.Queries;

public sealed record GetMyUserProfileQuery : IRequest<UserProfileDto>;
