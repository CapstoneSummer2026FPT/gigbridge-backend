using System.Text.Json;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
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
}
