using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record CancelScheduleCommand(Guid UserId, Guid ScheduleId, CancelScheduleRequest Request)
    : IRequest<ScheduleMutationResult>;
