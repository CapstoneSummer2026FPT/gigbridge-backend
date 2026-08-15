namespace Application.Common.InternalServices.MarketplaceAnalytics.Interfaces;
public interface IMarketplaceAnalyticsRecorder
{
    Task<Guid?> RecordSearchAsync(
        string actorIdentity,
        string? query,
        int resultCount,
        object filters,
        CancellationToken cancellationToken);

    Task RecordJobOpenAsync(
        string actorIdentity,
        Guid eventId,
        Guid jobPostId,
        Guid? searchEventId,
        CancellationToken cancellationToken);

    Task RecordJobSaveAsync(
        Guid userId,
        Guid jobPostId,
        DateTime occurredAt,
        CancellationToken cancellationToken);
}
