using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Features.Premium.Freelancer.RankProtection.DTOs;
using Application.Features.Premium.Freelancer.RankProtection.GetRankProtection;
using Domain.Entities;
using Domain.Enums.Notifications;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.RankProtection.ActivateRankProtection;

public sealed class ActivateRankProtectionCommandHandler : IRequestHandler<ActivateRankProtectionCommand, RankProtectionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPremiumAccessService _premium;
    private readonly IDateTimeService _clock;
    private readonly ICacheService _cache;
    private readonly INotificationService _notifications;

    public ActivateRankProtectionCommandHandler(
        IApplicationDbContext context,
        IPremiumAccessService premium,
        IDateTimeService clock,
        ICacheService cache,
        INotificationService notifications)
    {
        _context = context;
        _premium = premium;
        _clock = clock;
        _cache = cache;
        _notifications = notifications;
    }

    public async Task<RankProtectionDto> Handle(ActivateRankProtectionCommand command, CancellationToken cancellationToken)
    {
        var benefits = await _premium.GetPremiumBenefitsAsync(command.UserId, cancellationToken);
        if (!benefits.IsPremium || benefits.PremiumUntil is null)
            throw new ForbiddenAccessException("An active Premium Freelancer subscription is required.");
        var now = _clock.UtcNow;
        var starts = command.Request.StartsAt ?? now;
        if (starts < now.AddMinutes(-1) || command.Request.EndsAt <= starts)
            throw new BadRequestException("Rank protection dates are invalid.");
        if (command.Request.EndsAt > benefits.PremiumUntil)
            throw new BadRequestException("Rank protection cannot extend beyond the subscription end.");
        var profileId = await _context.Set<FreelancerProfile>().Where(x => x.UserId == command.UserId)
            .Select(x => (Guid?)x.FreelancerProfilesId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Freelancer profile does not exist.");
        if (await _context.Set<FreelancerRankProtection>().AnyAsync(x =>
            x.FreelancerProfileId == profileId && x.IsVacationModeEnabled && x.CancelledAt == null &&
            x.RankProtectionStartedAt < command.Request.EndsAt && x.RankProtectionEndsAt > starts, cancellationToken))
            throw new ConflictException("An active rank-protection window already overlaps this period.");

        var row = new FreelancerRankProtection
        {
            FreelancerRankProtectionsId = Guid.NewGuid(),
            FreelancerProfileId = profileId,
            IsVacationModeEnabled = true,
            RankProtectionStartedAt = starts,
            RankProtectionEndsAt = command.Request.EndsAt,
            RankProtectionReason = command.Request.Reason?.Trim(),
            CreatedAt = now
        };
        _context.Set<FreelancerRankProtection>().Add(row);
        _context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
        {
            PremiumUsageEventId = Guid.NewGuid(),
            Type = PremiumUsageEventType.RankProtection,
            UserId = command.UserId,
            IdempotencyKey = $"rank-protection:{row.FreelancerRankProtectionsId:N}",
            OccurredAt = now,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { startsAt = starts, endsAt = command.Request.EndsAt })
        });
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"premium:rank-protection:{command.UserId:N}", cancellationToken);
        await _notifications.CreateNotificationAsync(command.UserId, NotificationType.RankProtectionActivated,
            "Vacation Mode activated", $"Your ranking is protected until {row.RankProtectionEndsAt:O}.",
            row.FreelancerRankProtectionsId, nameof(FreelancerRankProtection), cancellationToken);
        return RankProtectionMapper.Map(row);
    }
}
