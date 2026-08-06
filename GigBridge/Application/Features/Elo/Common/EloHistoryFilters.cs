using Domain.Entities;

namespace Application.Features.Elo.Common;

/// <summary>
/// Filter categories for Elo history endpoints. The UI renders these as tabs
/// (All / Reviews / Disputes / Admin / Appeals / Points gained / Points lost);
/// unknown values degrade to <see cref="All"/> so a stale client never errors.
/// </summary>
public enum EloHistoryFilter
{
    All,
    Reviews,
    Disputes,
    Admin,
    Appeal,
    Gained,
    Lost
}

public static class EloHistoryFilters
{
    public static EloHistoryFilter ParseOrDefault(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? EloHistoryFilter.All
            : Enum.TryParse<EloHistoryFilter>(value, ignoreCase: true, out var parsed)
                ? parsed
                : EloHistoryFilter.All;

    /// <summary>
    /// Applies a filter to an Elo transaction query using the persisted Reason
    /// (always set) rather than SourceType, so legacy rows backfilled with
    /// SourceType=System still group correctly.
    /// </summary>
    public static IQueryable<UserEloPointTransaction> Apply(
        IQueryable<UserEloPointTransaction> query,
        EloHistoryFilter filter) => filter switch
    {
        EloHistoryFilter.Reviews => query.Where(x => x.Reason == 3 || x.Reason == 4 || x.Reason == 6 || x.Reason == 7),
        EloHistoryFilter.Disputes => query.Where(x => x.Reason == 8),
        EloHistoryFilter.Admin => query.Where(x => x.Reason == 9 || x.Reason == 10),
        EloHistoryFilter.Appeal => query.Where(x => x.Reason == 11),
        EloHistoryFilter.Gained => query.Where(x => x.PointsDelta > 0),
        EloHistoryFilter.Lost => query.Where(x => x.PointsDelta < 0),
        _ => query
    };
}
