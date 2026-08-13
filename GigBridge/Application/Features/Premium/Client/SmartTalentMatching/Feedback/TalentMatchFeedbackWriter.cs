using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Premium;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.SmartTalentMatching.Feedback;

public static class TalentMatchFeedbackWriter
{
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromDays(30);
    private const string UniqueViolationSqlState = "23505";
    private const string IdempotencyConstraintName = "UX_TalentMatchEvents_IdempotencyKey";

    public static async Task<bool> TryAddForRunAsync(
        IApplicationDbContext context,
        Guid matchRunId,
        Guid clientUserId,
        Guid? jobPostId,
        Guid freelancerProfileId,
        TalentMatchEventType eventType,
        string idempotencyKey,
        Guid? sourceEntityId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (matchRunId == Guid.Empty || freelancerProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        var belongsToRun = await context.Set<TalentMatchResult>()
            .AsNoTracking()
            .AnyAsync(result =>
                    result.TalentMatchRunId == matchRunId &&
                    result.FreelancerProfileId == freelancerProfileId &&
                    result.TalentMatchRun.ClientUserId == clientUserId &&
                    (!jobPostId.HasValue || result.TalentMatchRun.JobPostId == jobPostId.Value),
                cancellationToken);
        if (!belongsToRun)
        {
            return false;
        }

        return await TryAddAsync(context, matchRunId, freelancerProfileId, eventType,
            idempotencyKey, sourceEntityId, now, cancellationToken);
    }

    public static async Task<bool> TryAddLatestAttributedAsync(
        IApplicationDbContext context,
        Guid jobPostId,
        Guid freelancerProfileId,
        TalentMatchEventType eventType,
        Guid sourceEntityId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now - AttributionWindow;
        var runId = await context.Set<TalentMatchResult>()
            .AsNoTracking()
            .Where(result =>
                result.FreelancerProfileId == freelancerProfileId &&
                result.TalentMatchRun.JobPostId == jobPostId &&
                result.TalentMatchRun.CreatedAt >= cutoff)
            .OrderByDescending(result => result.TalentMatchRun.CreatedAt)
            .Select(result => (Guid?)result.TalentMatchRunId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!runId.HasValue)
        {
            return false;
        }

        var idempotencyKey = $"match:{runId.Value:N}:{eventType}:{sourceEntityId:N}";
        return await TryAddAsync(context, runId.Value, freelancerProfileId, eventType,
            idempotencyKey, sourceEntityId, now, cancellationToken);
    }

    public static async Task<bool> TrySaveAddedEventAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyConflict(exception))
        {
            // The competing request committed the same logical event first.
            return false;
        }
    }

    private static async Task<bool> TryAddAsync(
        IApplicationDbContext context,
        Guid matchRunId,
        Guid freelancerProfileId,
        TalentMatchEventType eventType,
        string idempotencyKey,
        Guid? sourceEntityId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedKey = idempotencyKey.Trim();
        if (normalizedKey.Length > 200)
        {
            normalizedKey = normalizedKey[..200];
        }

        var exists = await context.Set<TalentMatchEvent>()
            .AsNoTracking()
            .AnyAsync(item => item.IdempotencyKey == normalizedKey, cancellationToken);
        if (exists)
        {
            return false;
        }

        context.Set<TalentMatchEvent>().Add(new TalentMatchEvent
        {
            TalentMatchEventId = Guid.NewGuid(),
            TalentMatchRunId = matchRunId,
            FreelancerProfileId = freelancerProfileId,
            EventType = (int)eventType,
            SourceEntityId = sourceEntityId,
            IdempotencyKey = normalizedKey,
            CreatedAt = now
        });
        return true;
    }

    private static bool IsIdempotencyKeyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var exceptionType = current.GetType();
            var sqlState = exceptionType.GetProperty("SqlState")?.GetValue(current) as string;
            var constraintName = exceptionType.GetProperty("ConstraintName")?.GetValue(current) as string;
            if (string.Equals(sqlState, UniqueViolationSqlState, StringComparison.Ordinal) &&
                string.Equals(constraintName, IdempotencyConstraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
