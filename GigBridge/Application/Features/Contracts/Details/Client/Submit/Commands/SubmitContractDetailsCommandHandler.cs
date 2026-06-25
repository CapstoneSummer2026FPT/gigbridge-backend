using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Client.Submit.Commands;

public sealed class SubmitContractDetailsCommandHandler :
    IRequestHandler<SubmitContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public SubmitContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractWorkflowResponse> Handle(
        SubmitContractDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingContractDetails)
        {
            throw new BadRequestException("Only pending contract details can be submitted.");
        }

        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateMilestonesForSubmitOrPublish(contract, milestones);

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractConfirmation;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Contract details submitted for freelancer confirmation.",
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
                "ContractDetailsSubmitted",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }
}
