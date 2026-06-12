using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Freelancer.Confirm.Commands;

public sealed class ConfirmContractDetailsCommandHandler :
    IRequestHandler<ConfirmContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ConfirmContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractWorkflowResponse> Handle(
        ConfirmContractDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingContractConfirmation)
        {
            throw new BadRequestException("Only submitted contract details can be confirmed.");
        }

        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateTerms(contract);
        ContractDetailsValidator.ValidateMilestones(contract, milestones);

        var now = _dateTimeService.UtcNow;
        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(escrow => escrow.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                RequiredAmount = contract.TotalBudget,
                FundedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.PendingFunding,
                CreatedAt = now
            };
            _context.Set<ContractEscrow>().Add(escrow);
        }

        contract.Status = (int)ContractStatus.PendingSignature;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Contract details confirmed. Contract is ready for signatures.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, escrow.ContractEscrowId, null);
    }
}
