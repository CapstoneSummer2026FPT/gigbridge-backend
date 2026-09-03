using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiAssistant.Queries;

public sealed record GetAiChatBoxQuery(AiChatBoxRequestDto Request) : IRequest<AiChatBoxResponseDto>;
