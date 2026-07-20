using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalPlanMapper
{
    public static List<ProposalWorkBreakdownItemDto> ResolveWorkItems(
        IReadOnlyCollection<ProposalWorkBreakdownItemDto>? flatItems,
        IReadOnlyList<ProposalMilestonePlanDto> milestones)
    {
        if (flatItems is { Count: > 0 }) return flatItems.ToList();
        return milestones.SelectMany(milestone => milestone.WorkItems.Select(workItem => new ProposalWorkBreakdownItemDto
        {
            Id = workItem.Id,
            MilestonePlanId = workItem.MilestonePlanId,
            MilestoneOrderIndex = milestone.OrderIndex,
            Title = workItem.Title,
            Description = workItem.Description,
            Deliverables = workItem.Deliverables,
            EstimatedDuration = workItem.EstimatedDuration,
            OrderIndex = workItem.OrderIndex
        })).ToList();
    }

    public static ProposalWorkBreakdownItem ToEntity(
        Guid proposalId,
        ProposalWorkBreakdownItemDto item,
        int index,
        Guid? milestonePlanId = null)
    {
        return new ProposalWorkBreakdownItem
        {
            ProposalWorkBreakdownItemsId = Guid.NewGuid(),
            ProposalsId = proposalId,
            ProposalMilestonePlansId = milestonePlanId,
            Title = item.Title?.Trim() ?? string.Empty,
            Description = Clean(item.Description),
            Deliverables = Clean(item.Deliverables),
            EstimatedDuration = Clean(item.EstimatedDuration),
            OrderIndex = index
        };
    }

    public static ProposalMilestonePlan ToEntity(Guid proposalId, ProposalMilestonePlanDto item, int index)
    {
        return new ProposalMilestonePlan
        {
            ProposalMilestonePlansId = Guid.NewGuid(),
            ProposalsId = proposalId,
            Title = item.Title?.Trim() ?? string.Empty,
            Description = Clean(item.Description),
            Amount = item.Amount,
            EstimatedDuration = Clean(item.EstimatedDuration),
            Deliverables = Clean(item.Deliverables),
            AcceptanceCriteria = Clean(item.AcceptanceCriteria),
            OrderIndex = index
        };
    }

    public static ProposalWorkBreakdownItemDto ToDto(ProposalWorkBreakdownItem item) => new()
    {
        Id = item.ProposalWorkBreakdownItemsId,
        MilestonePlanId = item.ProposalMilestonePlansId,
        MilestoneOrderIndex = item.ProposalMilestonePlan?.OrderIndex,
        Title = item.Title,
        Description = item.Description,
        Deliverables = item.Deliverables,
        EstimatedDuration = item.EstimatedDuration,
        OrderIndex = item.OrderIndex
    };

    public static ProposalMilestonePlanDto ToDto(ProposalMilestonePlan item) => new()
    {
        Id = item.ProposalMilestonePlansId,
        Title = item.Title,
        Description = item.Description,
        Amount = item.Amount,
        EstimatedDuration = item.EstimatedDuration,
        Deliverables = item.Deliverables,
        AcceptanceCriteria = item.AcceptanceCriteria,
        OrderIndex = item.OrderIndex,
        WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select(ToDto).ToList()
    };

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
