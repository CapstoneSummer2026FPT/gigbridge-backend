using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class UpdateScheduleCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<UpdateScheduleCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(UpdateScheduleCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
