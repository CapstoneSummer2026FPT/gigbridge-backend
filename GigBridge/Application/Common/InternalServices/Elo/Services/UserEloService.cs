using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Elo.Services;
using Application.Features.Elo.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Elo;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.Elo.Services;
public class UserEloService : IUserEloService
{
    private const string UserSource = "User";
    private const string ReviewSource = "Review";
    private const string DisputeSource = "Dispute";
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UserEloService(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task InitializeNewUserAsync(User user, CancellationToken cancellationToken)
    {
        if (!IsEligibleRole(user.Role))
        {
            return;
        }

        var now = _dateTimeService.UtcNow;
        await EnsureScoreAsync(user.UserId, now, cancellationToken);
    }

    public async Task ApplyLoginActivityAsync(User user, CancellationToken cancellationToken)
    {
        if (!IsEligibleRole(user.Role))
        {
            return;
        }

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(user.UserId, now, cancellationToken);

        var previousLastActivityAt = score.LastActivityAt;
        var protectedDuration = user.Role == (int)UserRole.Freelancer
            ? await GetProtectedDurationAsync(user.UserId, previousLastActivityAt, now, cancellationToken)
            : TimeSpan.Zero;
        var effectiveInactiveFrom = previousLastActivityAt + protectedDuration;
        var inactivityPenalty = UserEloCalculator.CalculateInactivityPenalty(effectiveInactiveFrom, now);
        if (inactivityPenalty < 0 && ShouldApplyInactivityPenalty(score, previousLastActivityAt))
        {
            await ApplyDeltaAsync(
                score,
                inactivityPenalty,
                UserEloPointReason.InactivityPenalty,
                UserSource,
                user.UserId,
                CreateInactivityPenaltyKey(user.UserId, previousLastActivityAt),
                new
                {
                    inactiveFrom = previousLastActivityAt,
                    inactiveUntil = now,
                    protectedDays = protectedDuration.TotalDays,
                    requestedDelta = inactivityPenalty
                },
                now,
                cancellationToken,
                sourceType: (int)EloAdjustmentSourceType.System);

            score.LastInactivityPenaltyAt = now;
        }

        var returnBonus = UserEloCalculator.CalculateReturnBonus(previousLastActivityAt, now);
        if (returnBonus > 0 && ShouldApplyReturnBonus(score, previousLastActivityAt))
        {
            await ApplyDeltaAsync(
                score,
                returnBonus,
                UserEloPointReason.ReturnBonus,
                UserSource,
                user.UserId,
                CreateReturnBonusKey(user.UserId, previousLastActivityAt),
                new
                {
                    inactiveFrom = previousLastActivityAt,
                    returnedAt = now,
                    requestedDelta = returnBonus
                },
                now,
                cancellationToken,
                sourceType: (int)EloAdjustmentSourceType.System);

            score.LastReturnBonusAt = now;
        }

        score.LastActivityAt = now;
        score.UpdatedAt = now;
    }

    public async Task ApplyCompletedJobReviewAsync(
        Guid reviewId,
        Guid contractId,
        Guid revieweeId,
        decimal rating,
        CancellationToken cancellationToken)
    {
        var reviewee = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == revieweeId, cancellationToken);

        if (reviewee is null)
        {
            throw new NotFoundException("Reviewee does not exist.");
        }

        if (!IsEligibleRole(reviewee.Role))
        {
            return;
        }

        // Safety gate: Elo may only be applied once the job/contract is Completed.
        // A review-only or in-progress contract must not move Elo.
        var contractIsCompleted = await _context.Set<Contract>()
            .AsNoTracking()
            .AnyAsync(
                contract => contract.ContractsId == contractId &&
                            contract.Status == (int)ContractStatus.Completed,
                cancellationToken);

        if (!contractIsCompleted)
        {
            return;
        }

        // Reject ratings outside 1.0–5.0 or with more than one decimal place.
        EloCalculationService.EnsureValidRating(rating);

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(reviewee.UserId, now, cancellationToken);
        var delta = EloCalculationService.CalculateEloChange(rating);

        await ApplyDeltaAsync(
            score,
            delta,
            UserEloPointReason.CompletedJobReview,
            ReviewSource,
            reviewId,
            CreateCompletedJobReviewKey(contractId, revieweeId),
            new
            {
                rating,
                contractId,
                reviewId,
                requestedDelta = delta
            },
            now,
            cancellationToken,
            contractId: contractId,
            reviewId: reviewId,
            rating: rating,
            sourceType: (int)EloAdjustmentSourceType.Review);
    }

    public async Task<int> ApplyReviewModerationAsync(
        Guid reviewId,
        Guid revieweeId,
        Guid operationId,
        bool hide,
        CancellationToken cancellationToken)
    {
        var reviewee = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == revieweeId, cancellationToken)
            ?? throw new NotFoundException("Reviewee does not exist.");

        if (!IsEligibleRole(reviewee.Role))
        {
            return 0;
        }

        var transactions = _context.Set<UserEloPointTransaction>();
        int requestedDelta;
        if (hide)
        {
            var originalDelta = await transactions
                .Where(transaction =>
                    transaction.UserId == revieweeId &&
                    transaction.SourceEntityType == ReviewSource &&
                    transaction.SourceEntityId == reviewId &&
                    (transaction.Reason == (int)UserEloPointReason.JobCompletion ||
                     transaction.Reason == (int)UserEloPointReason.ReviewRating ||
                     transaction.Reason == (int)UserEloPointReason.CompletedJobReview))
                .SumAsync(transaction => transaction.PointsDelta, cancellationToken);
            requestedDelta = -originalDelta;
        }
        else
        {
            var moderationDelta = await transactions
                .Where(transaction =>
                    transaction.UserId == revieweeId &&
                    transaction.SourceEntityType == ReviewSource &&
                    transaction.SourceEntityId == reviewId &&
                    transaction.Reason == (int)UserEloPointReason.ReviewModeration)
                .SumAsync(transaction => transaction.PointsDelta, cancellationToken);
            requestedDelta = -moderationDelta;
        }

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(revieweeId, now, cancellationToken);
        var pointsBefore = score.CurrentPoints;
        var action = hide ? "hide" : "restore";
        await ApplyDeltaAsync(
            score,
            requestedDelta,
            UserEloPointReason.ReviewModeration,
            ReviewSource,
            reviewId,
            $"review-moderation:{reviewId}:{action}:{operationId}",
            new
            {
                reviewId,
                action,
                requestedDelta,
                operationId
            },
            now,
            cancellationToken,
            sourceType: (int)EloAdjustmentSourceType.Review);

        return score.CurrentPoints - pointsBefore;
    }

    /// <summary>
    /// Deducts the configured dispute-resolution penalty (default 50% of current
    /// points, rounded half-up) from <paramref name="userId"/>. The policy is read
    /// from PlatformSetting via <see cref="EloPolicy"/>. Idempotent per
    /// (dispute, user): a retry after a partial failure does not double-deduct.
    /// No-op when there is nothing to deduct.
    /// </summary>
    public async Task ApplyDisputeResolutionPenaltyAsync(
        Guid userId,
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("User does not exist.");

        if (!IsEligibleRole(user.Role))
        {
            return;
        }

        var policy = await EloPolicy.LoadAsync(_context, cancellationToken);
        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(userId, now, cancellationToken);
        var requestedDelta = UserEloCalculator.CalculatePenaltyDelta(score.CurrentPoints, policy.Mode, policy.Value);
        if (requestedDelta == 0)
        {
            return;
        }

        await ApplyDeltaAsync(
            score,
            requestedDelta,
            UserEloPointReason.DisputeResolutionPenalty,
            DisputeSource,
            disputeId,
            CreateDisputeResolutionPenaltyKey(disputeId, userId),
            new
            {
                disputeId,
                requestedDelta,
                policyMode = policy.Mode,
                penaltyValue = policy.Value
            },
            now,
            cancellationToken,
            sourceType: (int)EloAdjustmentSourceType.Dispute,
            mode: (int)policy.Mode);
    }

    /// <summary>
    /// Applies a manual administrator Elo adjustment (increase or decrease) through
    /// the same idempotent ledger workflow as every other Elo change. The adjustment
    /// is recorded with SourceType=Admin and the acting admin id, and is idempotent
    /// per <paramref name="requestId"/> so a client retry cannot double-apply.
    /// </summary>
    public async Task<UserEloPointTransaction?> ApplyAdminAdjustmentAsync(
        Guid adminId,
        Guid userId,
        int delta,
        string? note,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("User does not exist.");

        if (!IsEligibleRole(user.Role))
        {
            throw new BadRequestException("Elo adjustments can only be applied to client or freelancer accounts.");
        }

        if (delta == 0)
        {
            throw new BadRequestException("Adjustment delta must be non-zero.");
        }

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(userId, now, cancellationToken);
        var reason = delta > 0 ? UserEloPointReason.AdminIncrease : UserEloPointReason.AdminDecrease;

        return await ApplyDeltaAsync(
            score,
            delta,
            reason,
            "Admin",
            requestId,
            CreateAdminAdjustmentKey(requestId),
            new
            {
                adminId,
                note,
                requestedDelta = delta,
                requestId
            },
            now,
            cancellationToken,
            sourceType: (int)EloAdjustmentSourceType.Admin,
            appliedByAdminId: adminId);
    }

    /// <summary>
    /// Writes the correction transaction for a resolved Elo appeal. FullReversal
    /// negates the original transaction delta; PartialCorrection and
    /// CustomAdjustment use <paramref name="correctedDelta"/>. NoChange (and a
    /// zero requested delta) produce no transaction. Idempotent per appeal via the
    /// unique idempotency key, so a retry after partial failure never double-corrects.
    /// </summary>
    public async Task<UserEloPointTransaction?> ApplyAppealResolutionAsync(
        EloPointAppeal appeal,
        EloPointAppealResolution resolution,
        int? correctedDelta,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var original = await _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.UserEloPointTransactionsId == appeal.EloPointTransactionId,
                cancellationToken)
            ?? throw new NotFoundException("Appealed Elo transaction does not exist.");

        var delta = resolution switch
        {
            EloPointAppealResolution.FullReversal => -original.PointsDelta,
            EloPointAppealResolution.PartialCorrection => correctedDelta ?? 0,
            EloPointAppealResolution.CustomAdjustment => correctedDelta ?? 0,
            _ => 0
        };

        if (delta == 0)
        {
            return null;
        }

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(appeal.UserId, now, cancellationToken);

        return await ApplyDeltaAsync(
            score,
            delta,
            UserEloPointReason.AppealCorrection,
            "EloAppeal",
            appeal.EloPointAppealId,
            CreateAppealResolutionKey(appeal.EloPointAppealId),
            new
            {
                appealId = appeal.EloPointAppealId,
                resolution,
                requestedDelta = delta,
                correctedDelta,
                adminId
            },
            now,
            cancellationToken,
            sourceType: (int)EloAdjustmentSourceType.EloAppeal,
            eloAppealId: appeal.EloPointAppealId,
            appliedByAdminId: adminId);
    }

    private async Task<UserEloScore> EnsureScoreAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var scores = _context.Set<UserEloScore>();
        var score = scores.Local.FirstOrDefault(existingScore => existingScore.UserId == userId)
            ?? await scores.FirstOrDefaultAsync(existingScore => existingScore.UserId == userId, cancellationToken);

        if (score is not null)
        {
            return score;
        }

        score = new UserEloScore
        {
            UserEloScoresId = Guid.NewGuid(),
            UserId = userId,
            CurrentPoints = UserEloCalculator.DefaultPoints,
            LastActivityAt = now,
            CreatedAt = now
        };

        scores.Add(score);

        await AddTransactionIfMissingAsync(
            userId,
            UserEloCalculator.DefaultPoints,
            0,
            UserEloCalculator.DefaultPoints,
            UserEloPointReason.InitialGrant,
            UserSource,
            userId,
            CreateInitialGrantKey(userId),
            new
            {
                source = "application_initialization",
                requestedDelta = UserEloCalculator.DefaultPoints
            },
            now,
            cancellationToken,
            sourceType: (int)EloAdjustmentSourceType.System);

        return score;
    }

    private async Task<UserEloPointTransaction?> ApplyDeltaAsync(
        UserEloScore score,
        int requestedDelta,
        UserEloPointReason reason,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string idempotencyKey,
        object metadata,
        DateTime now,
        CancellationToken cancellationToken,
        Guid? contractId = null,
        Guid? reviewId = null,
        decimal? rating = null,
        int? sourceType = null,
        int? mode = null,
        Guid? eloAppealId = null,
        Guid? appliedByAdminId = null)
    {
        if (await TransactionExistsAsync(idempotencyKey, cancellationToken))
        {
            return null;
        }

        var pointsBefore = score.CurrentPoints;
        var pointsAfter = UserEloCalculator.ApplyDelta(pointsBefore, requestedDelta);
        var effectiveDelta = pointsAfter - pointsBefore;

        score.CurrentPoints = pointsAfter;
        score.UpdatedAt = now;

        return await AddTransactionIfMissingAsync(
            score.UserId,
            effectiveDelta,
            pointsBefore,
            pointsAfter,
            reason,
            sourceEntityType,
            sourceEntityId,
            idempotencyKey,
            metadata,
            now,
            cancellationToken,
            contractId: contractId,
            reviewId: reviewId,
            rating: rating,
            sourceType: sourceType,
            mode: mode,
            eloAppealId: eloAppealId,
            appliedByAdminId: appliedByAdminId);
    }

    private async Task<UserEloPointTransaction?> AddTransactionIfMissingAsync(
        Guid userId,
        int pointsDelta,
        int pointsBefore,
        int pointsAfter,
        UserEloPointReason reason,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string idempotencyKey,
        object metadata,
        DateTime now,
        CancellationToken cancellationToken,
        Guid? contractId = null,
        Guid? reviewId = null,
        decimal? rating = null,
        int? sourceType = null,
        int? mode = null,
        Guid? eloAppealId = null,
        Guid? appliedByAdminId = null)
    {
        if (await TransactionExistsAsync(idempotencyKey, cancellationToken))
        {
            return null;
        }

        var transaction = new UserEloPointTransaction
        {
            UserEloPointTransactionsId = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = pointsDelta,
            PointsBefore = pointsBefore,
            PointsAfter = pointsAfter,
            Reason = (int)reason,
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            IdempotencyKey = idempotencyKey,
            Metadata = JsonSerializer.Serialize(metadata),
            ContractId = contractId,
            ReviewId = reviewId,
            Rating = rating,
            SourceType = sourceType,
            Mode = mode,
            EloAppealId = eloAppealId,
            AppliedByAdminId = appliedByAdminId,
            CreatedAt = now
        };

        _context.Set<UserEloPointTransaction>().Add(transaction);
        return transaction;
    }

    private async Task<bool> TransactionExistsAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var transactions = _context.Set<UserEloPointTransaction>();
        return transactions.Local.Any(transaction => transaction.IdempotencyKey == idempotencyKey)
            || await transactions.AnyAsync(transaction => transaction.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    private async Task<TimeSpan> GetProtectedDurationAsync(
        Guid userId,
        DateTime inactiveFrom,
        DateTime inactiveUntil,
        CancellationToken cancellationToken)
    {
        var windows = await _context.Set<FreelancerRankProtection>()
            .AsNoTracking()
            .Where(item =>
                item.FreelancerProfile.UserId == userId &&
                item.RankProtectionStartedAt < inactiveUntil &&
                item.RankProtectionEndsAt > inactiveFrom)
            .Select(item => new
            {
                item.RankProtectionStartedAt,
                item.RankProtectionEndsAt,
                item.CancelledAt
            })
            .ToListAsync(cancellationToken);

        var ticks = windows.Sum(window =>
        {
            var start = window.RankProtectionStartedAt > inactiveFrom
                ? window.RankProtectionStartedAt : inactiveFrom;
            var recordedEnd = window.CancelledAt.HasValue &&
                              window.CancelledAt.Value < window.RankProtectionEndsAt
                ? window.CancelledAt.Value : window.RankProtectionEndsAt;
            var end = recordedEnd < inactiveUntil ? recordedEnd : inactiveUntil;
            return end > start ? (end - start).Ticks : 0;
        });
        return TimeSpan.FromTicks(Math.Min(ticks, (inactiveUntil - inactiveFrom).Ticks));
    }

    private static bool IsEligibleRole(int role)
    {
        return role == (int)UserRole.Client || role == (int)UserRole.Freelancer;
    }

    private static bool ShouldApplyInactivityPenalty(UserEloScore score, DateTime previousLastActivityAt)
    {
        return !score.LastInactivityPenaltyAt.HasValue
            || score.LastInactivityPenaltyAt.Value <= previousLastActivityAt;
    }

    private static bool ShouldApplyReturnBonus(UserEloScore score, DateTime previousLastActivityAt)
    {
        return !score.LastReturnBonusAt.HasValue
            || score.LastReturnBonusAt.Value <= previousLastActivityAt;
    }

    private static string CreateInitialGrantKey(Guid userId)
    {
        return $"initial:{userId}";
    }

    private static string CreateCompletedJobReviewKey(Guid contractId, Guid revieweeId)
    {
        return $"completed-job-review:{contractId}:{revieweeId}";
    }

    private static string CreateInactivityPenaltyKey(Guid userId, DateTime previousLastActivityAt)
    {
        return $"inactive:{userId}:{previousLastActivityAt:O}";
    }

    private static string CreateReturnBonusKey(Guid userId, DateTime previousLastActivityAt)
    {
        return $"return:{userId}:{previousLastActivityAt:O}";
    }

    private static string CreateDisputeResolutionPenaltyKey(Guid disputeId, Guid userId)
    {
        return $"dispute-resolution-penalty:{disputeId}:{userId}";
    }

    private static string CreateAdminAdjustmentKey(Guid requestId)
    {
        return $"elo-admin:{requestId}";
    }

    private static string CreateAppealResolutionKey(Guid appealId)
    {
        return $"elo-appeal-resolution:{appealId}";
    }
}
