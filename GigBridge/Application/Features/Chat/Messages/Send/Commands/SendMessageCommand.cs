using Application.Features.Chat.Messages.Send.DTOs;
using MediatR;

namespace Application.Features.Chat.Messages.Send.Commands;

public record SendMessageCommand(
    Guid UserId,
    SendMessageRequest Request) : IRequest<MessageResponse>;
