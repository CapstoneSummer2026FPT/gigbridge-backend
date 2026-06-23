using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record UpdateCounterProposalCommand(Guid UserId, Guid ScheduleId, CounterProposalRequest Request)
    : IRequest<ScheduleMutationResult>;
