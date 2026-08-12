using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Enums.Disputes;
using MediatR;

namespace Application.Features.Chat.Common.Messages.Send.Commands;

public record SendMessageCommand(
    Guid UserId,
    SendMessageRequest Request,
    string? ServerMetadata = null,
    DisputeMessageRecipient? DisputeRecipient = null) : IRequest<MessageResponse>;
