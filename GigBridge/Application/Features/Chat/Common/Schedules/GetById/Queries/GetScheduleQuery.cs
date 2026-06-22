using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record GetScheduleQuery(Guid UserId, Guid ScheduleId) : IRequest<ScheduleResponse>;
