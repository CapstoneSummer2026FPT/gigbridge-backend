using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.Accept.Commands;

public sealed class AcceptContractMilestonesCommandHandler :
    IRequestHandler<AcceptContractMilestonesCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public AcceptContractMilestonesCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractWorkflowResponse> Handle(
        AcceptContractMilestonesCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingSignature)
        {
            throw new BadRequestException("Milestones can only be accepted while the contract is pending signatures.");
        }

        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateMilestonesForSubmitOrPublish(contract, milestones);

        var fullySignedDocument = await _context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contract.ContractsId &&
                document.Status == (int)ESignDocumentStatus.FullySigned)
            .OrderByDescending(document => document.FinalizedAt ?? document.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (fullySignedDocument is null)
        {
            throw new BadRequestException("Contract must be fully signed before accepting milestones.");
        }

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
        else
        {
            escrow.RequiredAmount = contract.TotalBudget;
            escrow.FundedAmount = 0m;
            escrow.RequiredPercentage = 1.0m;
            escrow.Currency = string.IsNullOrWhiteSpace(escrow.Currency) ? "VND" : escrow.Currency;
            escrow.Status = (int)ContractEscrowStatus.PendingFunding;
            escrow.FundedAt = null;
            escrow.ReleasedAt = null;
        }

        contract.Status = (int)ContractStatus.PendingEscrow;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Milestones accepted. Waiting for client escrow funding.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var participantUserIds = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(p => p.Conversations.ContractsId == contract.ContractsId && p.LeftAt == null && p.DeletedAt == null)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (participantUserIds.Any())
        {
            await _chatRealtimeNotifier.SendUsersEventAsync(
                [.. participantUserIds],
                "ContractMilestonesAccepted",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(
            contract.ContractsId,
            contract.Status,
            escrow.ContractEscrowId,
            fullySignedDocument.EsignDocumentsId);
    }
}
