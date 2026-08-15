using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class CancelScheduleCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<CancelScheduleCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(CancelScheduleCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
