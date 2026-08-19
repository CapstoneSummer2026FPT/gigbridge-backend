using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class GetOngoingScheduleQueryHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<GetOngoingScheduleQuery, OngoingScheduleResponse>
{
    public Task<OngoingScheduleResponse> Handle(GetOngoingScheduleQuery query, CancellationToken ct) =>
        workflow.Handle(query, ct);
}
