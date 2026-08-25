using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces.Monitoring;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Project_API.Middleware;

namespace Test_Gigbridge_Backend.Project_API.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenClientAborts_DoesNotWriteAnErrorResponse()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromCanceled(cancellation.Token),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var reporter = new RecordingExceptionReporter();

        await middleware.InvokeAsync(context, [reporter]);

        Assert.Equal(0, responseBody.Length);
        Assert.Empty(reporter.Exceptions);
    }

    [Fact]
    public async Task InvokeAsync_WithValidationException_ReturnsFirstValidationErrorAsMessageAndErrors()
    {
        const string violation = "Job post appears to request or promote illegal drug-related work.";

        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(new Dictionary<string, string[]>
            {
                ["JobPostContent"] = new[] { violation }
            }),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context, []);

        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(responseBody);
        var root = document.RootElement;

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(violation, root.GetProperty("message").GetString());

        var errors = root.GetProperty("errors");
        Assert.Equal(
            violation,
            errors.GetProperty("JobPostContent")[0].GetString());
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidRefreshToken_Returns401WithoutWarningOrErrorLog()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/refresh";
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        var logger = new RecordingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new UnauthorizedAccessException("Invalid refresh token"),
            logger);
        var reporter = new RecordingExceptionReporter();

        await middleware.InvokeAsync(context, [reporter]);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(LogLevel.Information, logger.Levels);
        Assert.DoesNotContain(LogLevel.Warning, logger.Levels);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
        Assert.Empty(reporter.Exceptions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvokeAsync_WithReportableException_CapturesExactlyOnce(bool externalService)
    {
        Exception exception = externalService
            ? new ExternalServiceException("Provider failed")
            : new InvalidOperationException("Unexpected failure");
        var context = ContextWithResponseBody();
        var reporter = new RecordingExceptionReporter();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context, [reporter]);

        Assert.Same(exception, Assert.Single(reporter.Exceptions));
        Assert.Equal(
            externalService
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithBusinessException_DoesNotCaptureException()
    {
        var context = ContextWithResponseBody();
        var reporter = new RecordingExceptionReporter();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BadRequestException("Invalid request"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context, [reporter]);

        Assert.Empty(reporter.Exceptions);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenReporterFails_StillWritesOriginalErrorResponse()
    {
        var context = ContextWithResponseBody();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Unexpected failure"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context, [new ThrowingExceptionReporter()]);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(
            "An unexpected error occurred. Please try again later.",
            document.RootElement.GetProperty("message").GetString());
    }

    private static DefaultHttpContext ContextWithResponseBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class RecordingExceptionReporter : IExceptionReporter
    {
        public List<Exception> Exceptions { get; } = [];

        public void CaptureException(Exception exception) => Exceptions.Add(exception);
    }

    private sealed class ThrowingExceptionReporter : IExceptionReporter
    {
        public void CaptureException(Exception exception) =>
            throw new InvalidOperationException("Reporter unavailable");
    }

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
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}
