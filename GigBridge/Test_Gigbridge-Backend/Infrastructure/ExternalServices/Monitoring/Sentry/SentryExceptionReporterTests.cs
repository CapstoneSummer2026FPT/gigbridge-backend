using Infrastructure.ExternalServices.Monitoring.Sentry;
using NSubstitute;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Monitoring.Sentry;

public sealed class SentryExceptionReporterTests
{
    [Fact]
    public void CaptureException_ForwardsExceptionToCurrentSentryHub()
    {
        var hub = Substitute.For<global::Sentry.IHub>();
        var reporter = new SentryExceptionReporter(hub);
        var exception = new InvalidOperationException("failed");

        reporter.CaptureException(exception);

        hub.Received(1).CaptureException(exception);
    }
}
