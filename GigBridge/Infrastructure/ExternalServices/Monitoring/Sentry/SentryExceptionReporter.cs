using Application.Common.Interfaces.Monitoring;

namespace Infrastructure.ExternalServices.Monitoring.Sentry;

internal sealed class SentryExceptionReporter(IHub hub) : IExceptionReporter
{
    public void CaptureException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        hub.CaptureException(exception);
    }
}
