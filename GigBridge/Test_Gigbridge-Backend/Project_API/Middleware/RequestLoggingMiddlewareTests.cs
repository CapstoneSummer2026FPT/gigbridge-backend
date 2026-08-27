using Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Project_API.Hubs;
using Project_API.Middleware;
using Project_API.Services.SystemTracking;

namespace Test_Gigbridge_Backend.Project_API.Middleware;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenBusinessExceptionIsTranslated_RecordsTranslatedStatusCode()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/test-resource";
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var exceptionHandlingMiddleware = new ExceptionHandlingMiddleware(
            _ => throw new NotFoundException("Resource was not found."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var requestLoggingMiddleware = new RequestLoggingMiddleware(
            request => exceptionHandlingMiddleware.InvokeAsync(request, []),
            NullLogger<RequestLoggingMiddleware>.Instance);

        var clientProxy = Substitute.For<IClientProxy>();
        clientProxy
            .SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var hubClients = Substitute.For<IHubClients>();
        hubClients.All.Returns(clientProxy);
        var hubContext = Substitute.For<IHubContext<SystemTrackingHub>>();
        hubContext.Clients.Returns(hubClients);

        var trackingStore = new SystemTrackingStore();

        await requestLoggingMiddleware.InvokeAsync(context, trackingStore, hubContext);

        var snapshot = trackingStore.Snapshot("Testing", 10);
        var request = Assert.Single(snapshot.Requests);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, request.StatusCode);
        Assert.Equal(0, snapshot.Overview.ErrorRequests);
    }
}
