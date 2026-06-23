using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class UpdateCounterProposalCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<UpdateCounterProposalCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(UpdateCounterProposalCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
