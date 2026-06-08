using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Services;

public class UserEloService : IUserEloService
{
    private const string UserSource = "User";
    private const string ReviewSource = "Review";

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
        var inactivityPenalty = UserEloCalculator.CalculateInactivityPenalty(previousLastActivityAt, now);
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
                    requestedDelta = inactivityPenalty
                },
                now,
                cancellationToken);

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
                cancellationToken);

            score.LastReturnBonusAt = now;
        }

        score.LastActivityAt = now;
        score.UpdatedAt = now;
    }

    public async Task ApplyReviewScoreAsync(Guid reviewId, Guid revieweeId, int rating, CancellationToken cancellationToken)
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

        var now = _dateTimeService.UtcNow;
        var score = await EnsureScoreAsync(reviewee.UserId, now, cancellationToken);
        var completionDelta = UserEloCalculator.CalculateCompletionDelta(rating);
        var ratingDelta = UserEloCalculator.CalculateReviewRatingDelta(rating);

        if (completionDelta > 0)
        {
            await ApplyDeltaAsync(
                score,
                completionDelta,
                UserEloPointReason.JobCompletion,
                ReviewSource,
                reviewId,
                $"review:{reviewId}:{revieweeId}:completion",
                new
                {
                    rating,
                    component = "job_completion",
                    requestedDelta = completionDelta
                },
                now,
                cancellationToken);
        }

        await ApplyDeltaAsync(
            score,
            ratingDelta,
            UserEloPointReason.ReviewRating,
            ReviewSource,
            reviewId,
            $"review:{reviewId}:{revieweeId}:rating",
            new
            {
                rating,
                component = "review_rating",
                requestedDelta = ratingDelta
            },
            now,
            cancellationToken);
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
            cancellationToken);

        return score;
    }

    private async Task ApplyDeltaAsync(
        UserEloScore score,
        int requestedDelta,
        UserEloPointReason reason,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string idempotencyKey,
        object metadata,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await TransactionExistsAsync(idempotencyKey, cancellationToken))
        {
            return;
        }

        var pointsBefore = score.CurrentPoints;
        var pointsAfter = UserEloCalculator.ApplyDelta(pointsBefore, requestedDelta);
        var effectiveDelta = pointsAfter - pointsBefore;

        score.CurrentPoints = pointsAfter;
        score.UpdatedAt = now;

        await AddTransactionIfMissingAsync(
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
            cancellationToken);
    }

    private async Task AddTransactionIfMissingAsync(
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
        CancellationToken cancellationToken)
    {
        if (await TransactionExistsAsync(idempotencyKey, cancellationToken))
        {
            return;
        }

        _context.Set<UserEloPointTransaction>().Add(new UserEloPointTransaction
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
            CreatedAt = now
        });
    }

    private async Task<bool> TransactionExistsAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var transactions = _context.Set<UserEloPointTransaction>();
        return transactions.Local.Any(transaction => transaction.IdempotencyKey == idempotencyKey)
            || await transactions.AnyAsync(transaction => transaction.IdempotencyKey == idempotencyKey, cancellationToken);
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

    private static string CreateInactivityPenaltyKey(Guid userId, DateTime previousLastActivityAt)
    {
        return $"inactive:{userId}:{previousLastActivityAt:O}";
    }

    private static string CreateReturnBonusKey(Guid userId, DateTime previousLastActivityAt)
    {
        return $"return:{userId}:{previousLastActivityAt:O}";
    }
}
