using Application.Common.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.List.Queries;

public sealed class GetContractMilestonesQueryHandler :
    IRequestHandler<GetContractMilestonesQuery, IReadOnlyList<ContractMilestoneResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetContractMilestonesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ContractMilestoneResponse>> Handle(
        GetContractMilestonesQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            query.ContractId,
            cancellationToken);

        await MilestoneWorkflowGuard.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        return await _context.Set<Milestone>()
            .AsNoTracking()
            .Where(milestone => milestone.ContractsId == query.ContractId)
            .OrderBy(milestone => milestone.SortOrder)
            .ThenBy(milestone => milestone.CreatedAt)
            .Select(milestone => MilestoneWorkflowGuard.ToResponse(milestone))
            .ToListAsync(cancellationToken);
    }
}
