using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Profiles.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Subscriptions;
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

        var now = DateTime.UtcNow;

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
                IdentityOrTaxCode = user.IdentityOrTaxCode,
                PreferredLanguage = user.PreferredLanguage,
                Role = user.Role,
                IsPremium = (user.Role == (int)UserRole.Client || user.Role == (int)UserRole.Freelancer) &&
                    user.Subscriptions.Any(subscription =>
                        subscription.Status == SubscriptionStatus.Active &&
                        subscription.StartDate <= now &&
                        subscription.EndDate > now &&
                        subscription.SubscriptionPlans.IsActive == true &&
                        subscription.SubscriptionPlans.Price > 0 &&
                        (subscription.SubscriptionPlans.TargetRole == null ||
                         subscription.SubscriptionPlans.TargetRole == user.Role))
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile ?? throw new NotFoundException(nameof(User), currentUserId);
    }
}
