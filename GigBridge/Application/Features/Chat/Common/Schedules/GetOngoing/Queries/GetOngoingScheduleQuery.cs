using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record GetOngoingScheduleQuery(Guid UserId, Guid ConversationId) : IRequest<OngoingScheduleResponse>;
