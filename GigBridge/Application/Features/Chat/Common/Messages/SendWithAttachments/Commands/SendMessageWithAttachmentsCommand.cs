using Application.Features.Chat.Common.Messages.Send.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Messages.SendWithAttachments.Commands;

public sealed record ChatMessageFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record SendMessageWithAttachmentsCommand(
    Guid ConversationId,
    Guid UserId,
    string ClientMessageId,
    string? Content,
    IReadOnlyList<ChatMessageFile> Files) : IRequest<MessageResponse>;
