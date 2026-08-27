using Application.Common.InternalServices.Realtime.Models;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetInboxStatus.Queries;

public sealed record GetConversationInboxStatusQuery(Guid UserId) : IRequest<RealtimeStatusResponse>;
