using System.Text.Json;
using Application.Common.Exceptions;
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

        await middleware.InvokeAsync(context);

        Assert.Equal(0, responseBody.Length);
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

        await middleware.InvokeAsync(context);

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

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(LogLevel.Information, logger.Levels);
        Assert.DoesNotContain(LogLevel.Warning, logger.Levels);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
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
