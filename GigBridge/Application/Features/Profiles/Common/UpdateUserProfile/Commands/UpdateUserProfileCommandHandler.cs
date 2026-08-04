using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.Common.UpdateUserProfile.Commands;

public sealed class UpdateUserProfileCommandHandler
    : IRequestHandler<UpdateUserProfileCommand, UserProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public UpdateUserProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<UserProfileDto> Handle(
        UpdateUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(candidate => candidate.UserId == currentUserId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        var email = request.Dto.Email.Trim().ToLowerInvariant();
        var emailBelongsToAnotherUser = await _context.Set<User>()
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.UserId != currentUserId && candidate.Email == email,
                cancellationToken);

        if (emailBelongsToAnotherUser)
        {
            throw new ConflictException("Email is already in use.");
        }

        user.FullName = request.Dto.FullName.Trim();
        user.Email = email;
        user.Avatar = NormalizeOptional(request.Dto.Avatar);
        user.PhoneNumber = NormalizeOptional(request.Dto.PhoneNumber);
        user.PreferredLanguage = NormalizeOptional(request.Dto.PreferredLanguage)?.ToLowerInvariant();
        user.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new UserProfileDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Avatar = user.Avatar,
            PhoneNumber = user.PhoneNumber,
            PreferredLanguage = user.PreferredLanguage,
            Role = user.Role
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
