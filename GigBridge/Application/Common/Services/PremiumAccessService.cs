using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Services;

public sealed class PremiumAccessService : IPremiumAccessService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly IDateTimeService _clock;

    public PremiumAccessService(
        IApplicationDbContext context,
        ICacheService cache,
        IDateTimeService clock)
    {
        _context = context;
        _cache = cache;
        _clock = clock;
    }

    public async Task<bool> IsPremiumFreelancerAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        (await GetPremiumBenefitsAsync(userId, cancellationToken)).IsPremium;

    public async Task<PremiumBenefitsDto> GetPremiumBenefitsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var key = $"premium:access:{userId:N}";
        var cached = await _cache.GetAsync<PremiumBenefitsDto>(key, cancellationToken);
        if (cached is not null &&
            (!cached.IsPremium || cached.PremiumUntil > _clock.UtcNow))
            return cached;
        if (cached is not null)
            await _cache.RemoveAsync(key, cancellationToken);

        var user = await _context.Set<User>()
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.Role, item.IsEmailVerified })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            throw new NotFoundException("User", userId);

        var now = _clock.UtcNow;
        var subscription = await _context.Set<Subscription>()
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.Status == SubscriptionStatus.Active &&
                item.StartDate <= now &&
                item.EndDate > now &&
                item.SubscriptionPlans.IsActive == true &&
                item.SubscriptionPlans.Price > 0 &&
                (item.SubscriptionPlans.TargetRole == null ||
                 item.SubscriptionPlans.TargetRole == (int)UserRole.Freelancer))
            .OrderByDescending(item => item.EndDate)
            .Select(item => new { item.EndDate, item.SubscriptionPlans.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var isPremium = user.Role == (int)UserRole.Freelancer && subscription is not null;
        var result = new PremiumBenefitsDto(
            isPremium,
            user.IsEmailVerified,
            isPremium && user.IsEmailVerified,
            isPremium ? subscription!.EndDate : null,
            isPremium ? subscription!.Name : null);

        await _cache.SetAsync(key, result, CacheDuration, cancellationToken);
        return result;
    }

    public async Task RequirePremiumFreelancerAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsPremiumFreelancerAsync(userId, cancellationToken))
            throw new ForbiddenAccessException("An active Premium Freelancer subscription is required.");
    }
}
