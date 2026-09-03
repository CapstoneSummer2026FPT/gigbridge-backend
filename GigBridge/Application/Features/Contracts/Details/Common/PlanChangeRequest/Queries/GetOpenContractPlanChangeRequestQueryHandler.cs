using Application.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Contracts.Details.Common.PlanChangeRequest.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Common.PlanChangeRequest.Queries;

public sealed class GetOpenContractPlanChangeRequestQueryHandler :
    IRequestHandler<GetOpenContractPlanChangeRequestQuery, ContractPlanChangeRequestDto?>
{
    private readonly IApplicationDbContext _context;

    public GetOpenContractPlanChangeRequestQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContractPlanChangeRequestDto?> Handle(
        GetOpenContractPlanChangeRequestQuery query,
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

        var request = await ContractPlanChangeRequests.GetOpenAsync(
            _context,
            query.ContractId,
            cancellationToken);

        if (request is null)
        {
            return null;
        }

        var requestedByName = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == request.RequestedByUserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return new ContractPlanChangeRequestDto(
            request.ContractPlanChangeRequestId,
            request.ContractsId,
            request.RequestedByUserId,
            requestedByName ?? "Freelancer",
            request.Reason,
            request.AffectedMilestoneIds,
            request.AffectedWorkItemIds,
            request.Origin,
            request.CreatedAt);
    }
}
