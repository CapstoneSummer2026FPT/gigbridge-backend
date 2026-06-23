using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record AcceptScheduleCommand(Guid UserId, Guid ScheduleId, ScheduleVersionRequest Request)
    : IRequest<ScheduleMutationResult>;
