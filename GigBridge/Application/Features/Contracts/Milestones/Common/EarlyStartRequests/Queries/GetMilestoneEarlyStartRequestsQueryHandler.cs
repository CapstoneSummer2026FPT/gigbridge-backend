using Application.Common.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.EarlyStartRequests.Queries;

public sealed class GetMilestoneEarlyStartRequestsQueryHandler
    : IRequestHandler<GetMilestoneEarlyStartRequestsQuery, IReadOnlyList<MilestoneEarlyStartRequestDto>>
{
    private readonly IApplicationDbContext _context;
    public GetMilestoneEarlyStartRequestsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<MilestoneEarlyStartRequestDto>> Handle(
        GetMilestoneEarlyStartRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, query.ContractId, cancellationToken);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, query.UserId, cancellationToken);
        return await _context.Set<MilestoneEarlyStartRequest>()
            .AsNoTracking()
            .Where(item => item.ContractsId == query.ContractId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new MilestoneEarlyStartRequestDto(item.MilestoneEarlyStartRequestId, item.ContractsId,
                item.MilestonesId, item.Reason, item.ResponseNote, item.Status, item.CreatedAt, item.RespondedAt))
            .ToListAsync(cancellationToken);
    }
}
