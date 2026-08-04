using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.Common.GetMyUserProfile.Queries;

public sealed class GetMyUserProfileQueryHandler
    : IRequestHandler<GetMyUserProfileQuery, UserProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyUserProfileQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UserProfileDto> Handle(
        GetMyUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var profile = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == currentUserId)
            .Select(user => new UserProfileDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Avatar = user.Avatar,
                PhoneNumber = user.PhoneNumber,
                PreferredLanguage = user.PreferredLanguage,
                Role = user.Role
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile ?? throw new NotFoundException(nameof(User), currentUserId);
    }
}
