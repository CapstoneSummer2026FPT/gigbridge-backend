using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Time;
using Application.Features.Profiles.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
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
            .Include(candidate => candidate.Subscriptions)
                .ThenInclude(subscription => subscription.SubscriptionPlans)
            .FirstOrDefaultAsync(candidate => candidate.UserId == currentUserId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        var email = request.Dto.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Email cannot be changed through profile updates. Complete the secure email-change verification flow instead.");
        }

        user.FullName = request.Dto.FullName.Trim();
        user.Avatar = NormalizeOptional(request.Dto.Avatar);
        user.PhoneNumber = NormalizeOptional(request.Dto.PhoneNumber);
        if (request.Dto.IdentityOrTaxCode is not null)
        {
            user.IdentityOrTaxCode = NormalizeIdentityCode(request.Dto.IdentityOrTaxCode);
        }
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
            IdentityOrTaxCode = user.IdentityOrTaxCode,
            PreferredLanguage = user.PreferredLanguage,
            Role = user.Role,
            IsPremium = user.IsPremium
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeIdentityCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ContractIdentityCode.Normalize(value);
    }
}
