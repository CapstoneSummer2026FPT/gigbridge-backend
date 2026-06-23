using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record RejectScheduleCommand(Guid UserId, Guid ScheduleId, ScheduleVersionRequest Request)
    : IRequest<ScheduleMutationResult>;
