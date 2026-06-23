using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record CreateScheduleCommand(Guid UserId, CreateScheduleRequest Request) : IRequest<ScheduleMutationResult>;
