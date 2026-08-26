namespace Application.Common.Interfaces.Monitoring;

public interface IExceptionReporter
{
    void CaptureException(Exception exception);
}
