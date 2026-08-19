using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class RetryScheduleMeetingCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<RetryScheduleMeetingCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(RetryScheduleMeetingCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
