using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record UpdateScheduleCommand(Guid UserId, Guid ScheduleId, UpdateScheduleRequest Request)
    : IRequest<ScheduleMutationResult>;
