using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.AiAssistant.Queries;
using NSubstitute;
using Xunit;

namespace Test_Gigbridge_Backend.Application.Features.AiAssistant;

public class GetAiChatBoxQueryHandlerTests
{
    private readonly IAiServiceClient _aiServiceClient;
    private readonly GetAiChatBoxQueryHandler _handler;

    public GetAiChatBoxQueryHandlerTests()
    {
        _aiServiceClient = Substitute.For<IAiServiceClient>();
        _handler = new GetAiChatBoxQueryHandler(_aiServiceClient);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsResponseFromService()
    {
        // Arrange
        var request = new AiChatBoxRequestDto { Question = "Hello" };
        var expectedResponse = new AiChatBoxResponseDto { Answer = "Hi there!" };
        var query = new GetAiChatBoxQuery(request);

        _aiServiceClient.QueryChatBoxAsync(request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.Answer, result.Answer);
        await _aiServiceClient.Received(1).QueryChatBoxAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ServiceThrowsHttpRequestException_ThrowsExternalServiceException()
    {
        // Arrange
        var request = new AiChatBoxRequestDto { Question = "Hello" };
        var query = new GetAiChatBoxQuery(request);

        _aiServiceClient.QueryChatBoxAsync(request, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiChatBoxResponseDto>(new HttpRequestException("Network failure")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExternalServiceException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Contains("AI service is temporarily unavailable", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }
}
