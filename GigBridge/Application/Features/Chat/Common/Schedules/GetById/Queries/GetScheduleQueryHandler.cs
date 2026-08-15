using Application.Common.InternalServices.Chat.Services;
using MediatR;

namespace Application.Features.Chat.Common.Schedules;

public sealed class GetScheduleQueryHandler(ScheduleWorkflowService workflow)
    : IRequestHandler<GetScheduleQuery, ScheduleResponse>
{
    public Task<ScheduleResponse> Handle(GetScheduleQuery query, CancellationToken ct) => workflow.Handle(query, ct);
}
