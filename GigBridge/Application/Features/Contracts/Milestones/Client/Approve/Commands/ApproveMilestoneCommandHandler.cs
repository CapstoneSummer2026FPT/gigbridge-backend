using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Client.Approve.Commands;

public sealed class ApproveMilestoneCommandHandler :
    IRequestHandler<ApproveMilestoneCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ApproveMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractMilestoneResponse> Handle(
        ApproveMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureClientAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.Submitted)
        {
            throw new BadRequestException("Only submitted milestones can be approved.");
        }

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(e => e.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            throw new NotFoundException("Contract escrow does not exist. The contract must be funded before milestones can be approved.");
        }

        if (escrow.Status != (int)ContractEscrowStatus.Funded &&
            escrow.Status != (int)ContractEscrowStatus.PartiallyReleased &&
            escrow.Status != (int)ContractEscrowStatus.Released)
        {
            throw new BadRequestException("Escrow must be funded before milestone approval.");
        }

        var now = _dateTimeService.UtcNow;
        milestone.Status = (int)MilestoneStatus.Approved;
        milestone.ApprovedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone approved: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MilestoneWorkflowGuard.ToResponse(milestone);
    }
}
