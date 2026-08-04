using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public record CreateCounterProposalCommand(Guid UserId, Guid ScheduleId, CounterProposalRequest Request)
    : IRequest<ScheduleMutationResult>;
