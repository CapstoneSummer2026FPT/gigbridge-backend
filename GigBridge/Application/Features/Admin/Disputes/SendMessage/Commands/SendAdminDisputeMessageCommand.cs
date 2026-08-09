using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.SendMessage.Commands;

public sealed record AdminDisputeMessageFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record SendAdminDisputeMessageCommand(
    Guid DisputeId,
    Guid ConversationId,
    Guid AdminId,
    string? Content,
    IReadOnlyList<AdminDisputeMessageFile> Files,
    DisputeMessageRecipient Recipient) : IRequest<MessageResponse>;
