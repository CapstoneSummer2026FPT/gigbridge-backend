using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class AcceptScheduleCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<AcceptScheduleCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(AcceptScheduleCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
