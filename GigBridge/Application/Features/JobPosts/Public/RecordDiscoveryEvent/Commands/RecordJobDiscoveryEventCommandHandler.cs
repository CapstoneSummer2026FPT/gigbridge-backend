using Application.Features.MarketplaceAnalytics.Common.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobPosts.Public.RecordDiscoveryEvent.Commands;

public sealed class RecordJobDiscoveryEventCommandHandler(
    IMarketplaceAnalyticsRecorder analytics,
    ILogger<RecordJobDiscoveryEventCommandHandler> logger)
    : IRequestHandler<RecordJobDiscoveryEventCommand, RecordJobDiscoveryEventResult>
{
    public async Task<RecordJobDiscoveryEventResult> Handle(
        RecordJobDiscoveryEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            await analytics.RecordJobOpenAsync(
                request.ActorIdentity,
                request.EventId,
                request.JobPostId,
                request.SearchEventId,
                cancellationToken);
            return new(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to record job discovery event {EventId}.", request.EventId);
            return new(false);
        }
    }
}
