using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class RejectScheduleCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<RejectScheduleCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(RejectScheduleCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
