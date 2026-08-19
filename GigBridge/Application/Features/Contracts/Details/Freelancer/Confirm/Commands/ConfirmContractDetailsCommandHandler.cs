using Application.Common.InternalServices.ESign.Services;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Freelancer.Confirm.Commands;

public sealed class ConfirmContractDetailsCommandHandler :
    IRequestHandler<ConfirmContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IContractEsignDocumentGenerator _documentGenerator;
    private readonly IUserAuditLogService _userAuditLog;

    public ConfirmContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IContractEsignDocumentGenerator documentGenerator,
        IUserAuditLogService userAuditLog)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _documentGenerator = documentGenerator;
        _userAuditLog = userAuditLog;
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
            .Include(item => item.WorkItems)
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateMilestonesForSubmitOrPublish(contract, milestones);

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
        var (document, _) = await ContractEsignRenderer.EnsureDocumentAsync(
            _context, _documentGenerator, contract, now, cancellationToken);

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Contract details confirmed. Contract is ready for signatures.",
            now,
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            UserRole.Freelancer,
            AuditUserActionType.ConfirmedParticipation,
            contract.ContractsId,
            "Freelancer confirmed participation in the project.");

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
                "ContractDetailsConfirmed",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, escrow.ContractEscrowId, document.EsignDocumentsId);
    }
}
