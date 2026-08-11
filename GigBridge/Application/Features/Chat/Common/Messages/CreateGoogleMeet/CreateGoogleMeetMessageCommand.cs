using Application.Features.Chat.Common.Messages.Send.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Messages.CreateGoogleMeet;

public record CreateGoogleMeetMessageRequest(Guid ConversationId, string ClientMessageId);

public record CreateGoogleMeetMessageCommand(
    Guid UserId,
    CreateGoogleMeetMessageRequest Request) : IRequest<MessageResponse>;
