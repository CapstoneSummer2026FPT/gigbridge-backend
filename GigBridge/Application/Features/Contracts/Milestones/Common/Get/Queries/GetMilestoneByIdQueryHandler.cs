using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.Get.Queries;

public sealed class GetMilestoneByIdQueryHandler :
    IRequestHandler<GetMilestoneByIdQuery, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;

    public GetMilestoneByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContractMilestoneResponse> Handle(
        GetMilestoneByIdQuery query,
        CancellationToken cancellationToken)
    {
        var milestone = await _context.Set<Milestone>()
            .Include(m => m.MilestoneAttachments)
            .FirstOrDefaultAsync(m => m.MilestonesId == query.MilestoneId, cancellationToken);

        if (milestone == null)
        {
            throw new NotFoundException("Milestone does not exist.");
        }

        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            milestone.ContractsId,
            cancellationToken);

        await MilestoneWorkflowGuard.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        return MilestoneWorkflowGuard.ToResponse(milestone);
    }
}
