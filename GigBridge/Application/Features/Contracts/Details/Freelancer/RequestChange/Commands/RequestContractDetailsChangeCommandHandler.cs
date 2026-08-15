using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Freelancer.RequestChange.Commands;

public sealed class RequestContractDetailsChangeCommandHandler :
    IRequestHandler<RequestContractDetailsChangeCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public RequestContractDetailsChangeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractWorkflowResponse> Handle(
        RequestContractDetailsChangeCommand command,
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
            throw new BadRequestException("Only submitted contract details can receive change requests.");
        }

        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            throw new BadRequestException("A reason is required when requesting contract plan changes.");
        }
        var milestoneIds = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .Select(item => item.MilestonesId)
            .ToListAsync(cancellationToken);
        if ((command.Request.AffectedMilestoneIds ?? []).Except(milestoneIds).Any())
        {
            throw new BadRequestException("One or more affected milestones do not belong to this contract.");
        }
        var workItemIds = await _context.Set<ContractWorkItem>()
            .Where(item => milestoneIds.Contains(item.MilestonesId))
            .Select(item => item.ContractWorkItemId)
            .ToListAsync(cancellationToken);
        if ((command.Request.AffectedWorkItemIds ?? []).Except(workItemIds).Any())
        {
            throw new BadRequestException("One or more affected work items do not belong to this contract.");
        }

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractDetails;
        contract.UpdatedAt = now;

        var message = $"Contract plan changes requested: {command.Request.Reason.Trim()}" +
            $" (milestones: {(command.Request.AffectedMilestoneIds ?? []).Count}, work items: {(command.Request.AffectedWorkItemIds ?? []).Count}).";

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            message,
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
                "ContractDetailsChangeRequested",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }
}
