using MediatR;

namespace Application.Features.Disputes.Client.RemainingJobPostPlan.Queries;

public sealed record GetDisputeRemainingJobPostPlanQuery(Guid DisputeId, Guid UserId)
    : IRequest<DisputeRemainingJobPostPlanResponse>;
