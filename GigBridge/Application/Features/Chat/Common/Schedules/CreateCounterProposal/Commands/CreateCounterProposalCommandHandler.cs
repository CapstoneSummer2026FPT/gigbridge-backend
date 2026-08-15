using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class CreateCounterProposalCommandHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<CreateCounterProposalCommand, ScheduleMutationResult>
{
    public Task<ScheduleMutationResult> Handle(CreateCounterProposalCommand command, CancellationToken ct) =>
        workflow.Handle(command, ct);
}
