using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalPlanMapper
{
    public static ProposalWorkBreakdownItem ToEntity(Guid proposalId, ProposalWorkBreakdownItemDto item, int index)
    {
        return new ProposalWorkBreakdownItem
        {
            ProposalWorkBreakdownItemsId = Guid.NewGuid(),
            ProposalsId = proposalId,
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
        OrderIndex = item.OrderIndex
    };

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
