using Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Test_Gigbridge_Backend.Application.Common;

public sealed class UnhandledExceptionBehaviourTests
{
    [Fact]
    public async Task Handle_WhenPipelineTokenIsCanceled_DoesNotLogAnError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var logger = new RecordingLogger<TestRequest>();
        var behavior = new UnhandledExceptionBehaviour<TestRequest, string>(logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new TestRequest(),
            _ => Task.FromCanceled<string>(cancellation.Token),
            cancellation.Token));

        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
        Assert.Contains(LogLevel.Debug, logger.Levels);
    }

    [Fact]
    public async Task Handle_WhenCancellationIsNotFromPipelineToken_StillLogsAnError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var logger = new RecordingLogger<TestRequest>();
        var behavior = new UnhandledExceptionBehaviour<TestRequest, string>(logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new TestRequest(),
            _ => Task.FromCanceled<string>(cancellation.Token),
            CancellationToken.None));

        Assert.Contains(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task Handle_WhenAuthenticationIsRejected_LogsDebugInsteadOfWarning()
    {
        var logger = new RecordingLogger<TestRequest>();
        var behavior = new UnhandledExceptionBehaviour<TestRequest, string>(logger);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => behavior.Handle(
            new TestRequest(),
            _ => throw new UnauthorizedAccessException("Invalid refresh token"),
            CancellationToken.None));

        Assert.Contains(LogLevel.Debug, logger.Levels);
        Assert.DoesNotContain(LogLevel.Warning, logger.Levels);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    private sealed record TestRequest : IRequest<string>;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
        }
    }
}
