using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Client.Update.Commands;

public sealed class UpdateContractDetailsCommandHandler :
    IRequestHandler<UpdateContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public UpdateContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractWorkflowResponse> Handle(
        UpdateContractDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingContractDetails &&
            contract.Status != (int)ContractStatus.PendingFreelancerSelection &&
            contract.Status != (int)ContractStatus.InNegotiation)
        {
            throw new BadRequestException("Contract details can only be edited while in pending selection, negotiation, or pending details.");
        }

        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        var now = _dateTimeService.UtcNow;
        contract.UpdatedAt = now;
        contract.RevisionNumber += 1;

        var newMilestones = command.Request.Milestones
            .Select((request, index) =>
            {
                var milestone = new Milestone
                {
                    MilestonesId = request.MilestoneId ?? Guid.NewGuid(),
                    ContractsId = contract.ContractsId,
                    Title = request.Title,
                    Description = request.Description,
                    Amount = request.Amount,
                    EstimatedDuration = request.EstimatedDuration,
                    DueDate = request.DueDate,
                    Deliverables = request.Deliverables,
                    AcceptanceCriteria = request.AcceptanceCriteria,
                    SortOrder = request.SortOrder ?? index,
                    Status = (int)MilestoneStatus.Pending,
                    CreatedAt = now
                };
                milestone.WorkItems = (request.WorkItems ?? [])
                    .OrderBy(item => item.OrderIndex)
                    .Select((item, workIndex) => new ContractWorkItem
                    {
                        ContractWorkItemId = item.WorkItemId ?? Guid.NewGuid(),
                        MilestonesId = milestone.MilestonesId,
                        Title = item.Title.Trim(),
                        Description = Clean(item.Description),
                        Deliverables = Clean(item.Deliverables),
                        EstimatedDuration = Clean(item.EstimatedDuration),
                        OrderIndex = workIndex,
                        Status = (int)ContractWorkItemStatus.Todo,
                        CreatedAt = now
                    })
                    .ToList();
                return milestone;
            })
            .ToList();

        ContractDetailsValidator.ValidateMilestoneDraft(newMilestones);
        ContractDetailsValidator.ValidateMilestoneTotalDoesNotExceedBudget(contract, newMilestones);

        var existingMilestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        _context.Set<Milestone>().RemoveRange(existingMilestones);
        foreach (var milestone in newMilestones)
        {
            _context.Set<Milestone>().Add(milestone);
        }

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
                "ContractDraftUpdated",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
