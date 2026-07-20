using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;

namespace Application.Features.JobPosts.Common;

internal static class JobPostPlanProjection
{
    public static List<JobPostMilestonePlanDto> ToDtos(IEnumerable<JobPostMilestonePlan> plans) =>
        plans.OrderBy(plan => plan.OrderIndex).Select(plan => new JobPostMilestonePlanDto
        {
            Id = plan.JobPostMilestonePlanId,
            Title = plan.Title,
            Description = plan.Description,
            Amount = plan.Amount,
            EstimatedDuration = plan.EstimatedDuration,
            Deliverables = plan.Deliverables,
            AcceptanceCriteria = plan.AcceptanceCriteria,
            OrderIndex = plan.OrderIndex,
            WorkItems = plan.WorkItems.OrderBy(item => item.OrderIndex).Select(item => new JobPostWorkItemDto
            {
                Id = item.JobPostWorkItemId,
                Title = item.Title,
                Description = item.Description,
                Deliverables = item.Deliverables,
                EstimatedDuration = item.EstimatedDuration,
                OrderIndex = item.OrderIndex
            }).ToList()
        }).ToList();
}
