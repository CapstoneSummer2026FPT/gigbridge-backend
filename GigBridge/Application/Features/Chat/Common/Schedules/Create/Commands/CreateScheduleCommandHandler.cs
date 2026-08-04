using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class CreateScheduleCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<CreateScheduleCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(CreateScheduleCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
