using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record RetryScheduleMeetingCommand(Guid UserId, Guid ScheduleId) : IRequest<ScheduleMutationResult>;
