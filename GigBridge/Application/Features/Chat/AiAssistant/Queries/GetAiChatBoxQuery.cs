using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.Chat.AiAssistant.Queries;

public sealed record GetAiChatBoxQuery(AiChatBoxRequestDto Request) : IRequest<AiChatBoxResponseDto>;

public sealed class GetAiChatBoxQueryHandler(IAiServiceClient aiServiceClient)
    : IRequestHandler<GetAiChatBoxQuery, AiChatBoxResponseDto>
{
    public async Task<AiChatBoxResponseDto> Handle(
        GetAiChatBoxQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await aiServiceClient.QueryChatBoxAsync(query.Request, cancellationToken);
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new ExternalServiceException(
                "AI service is temporarily unavailable. Please try again later.", exception);
        }
    }
}
