using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Project_API.Hubs;
using Project_API.Services.SystemTracking;
namespace Project_API.Middleware;
public class RequestLoggingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger) {
        _next = next;
        _logger = logger;
    }
    public async Task InvokeAsync(
        HttpContext context,
        SystemTrackingStore trackingStore,
        IHubContext<SystemTrackingHub> trackingHub) {
        var stopwatch = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            stopwatch.Stop();
            trackingStore.Record(context, stopwatch.ElapsedMilliseconds, statusCode);
            _logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds);

            if (!context.Request.Path.StartsWithSegments("/api/admin/system-tracking"))
            {
                try
                {
                    await trackingHub.Clients.All.SendAsync(
                        SystemTrackingHub.SnapshotUpdatedEvent,
                        new
                        {
                            generatedAt = DateTimeOffset.UtcNow,
                            requestId = Activity.Current?.Id ?? context.TraceIdentifier
                        },
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not broadcast the system tracking update.");
                }
            }
        }
    }
}
