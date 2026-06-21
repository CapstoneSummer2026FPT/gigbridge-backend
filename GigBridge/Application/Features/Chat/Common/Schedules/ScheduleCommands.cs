using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record CreateScheduleCommand(Guid UserId, CreateScheduleRequest Request) : IRequest<ScheduleMutationResult>;
public record UpdateScheduleCommand(Guid UserId, Guid ScheduleId, UpdateScheduleRequest Request) : IRequest<ScheduleMutationResult>;
public record CancelScheduleCommand(Guid UserId, Guid ScheduleId, CancelScheduleRequest Request) : IRequest<ScheduleMutationResult>;
public record GetScheduleQuery(Guid UserId, Guid ScheduleId) : IRequest<ScheduleResponse>;
public record GetOngoingScheduleQuery(Guid UserId, Guid ConversationId) : IRequest<OngoingScheduleResponse>;
