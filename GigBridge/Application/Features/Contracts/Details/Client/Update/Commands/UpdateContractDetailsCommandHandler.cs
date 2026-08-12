using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
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
        ValidateUniqueIdentifiers(newMilestones);

        var existingMilestones = await _context.Set<Milestone>()
            .Include(milestone => milestone.WorkItems)
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        var existingMilestonesById = existingMilestones
            .ToDictionary(milestone => milestone.MilestonesId);
        var existingWorkItems = existingMilestones
            .SelectMany(milestone => milestone.WorkItems)
            .ToList();
        var existingWorkItemsById = existingWorkItems
            .ToDictionary(workItem => workItem.ContractWorkItemId);
        var retainedMilestoneIds = new HashSet<Guid>();
        var retainedWorkItemIds = new HashSet<Guid>();

        foreach (var milestoneDraft in newMilestones)
        {
            retainedMilestoneIds.Add(milestoneDraft.MilestonesId);

            Milestone milestone;
            if (existingMilestonesById.TryGetValue(milestoneDraft.MilestonesId, out var existingMilestone))
            {
                milestone = existingMilestone;
                ApplyMilestoneDraft(milestone, milestoneDraft, now);
            }
            else
            {
                milestone = CreateMilestone(milestoneDraft, contract.ContractsId, now);
                _context.Set<Milestone>().Add(milestone);
            }

            foreach (var workItemDraft in milestoneDraft.WorkItems)
            {
                retainedWorkItemIds.Add(workItemDraft.ContractWorkItemId);

                if (existingWorkItemsById.TryGetValue(workItemDraft.ContractWorkItemId, out var existingWorkItem))
                {
                    ApplyWorkItemDraft(existingWorkItem, workItemDraft, milestone, now);
                    continue;
                }

                var workItem = CreateWorkItem(workItemDraft, milestone, now);
                _context.Set<ContractWorkItem>().Add(workItem);
            }
        }

        _context.Set<ContractWorkItem>().RemoveRange(
            existingWorkItems.Where(workItem => !retainedWorkItemIds.Contains(workItem.ContractWorkItemId)));
        _context.Set<Milestone>().RemoveRange(
            existingMilestones.Where(milestone => !retainedMilestoneIds.Contains(milestone.MilestonesId)));

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

    private static void ValidateUniqueIdentifiers(IReadOnlyCollection<Milestone> milestones)
    {
        if (milestones.Select(milestone => milestone.MilestonesId).Distinct().Count() != milestones.Count)
        {
            throw new BadRequestException("Milestone IDs must be unique.");
        }

        var workItemIds = milestones
            .SelectMany(milestone => milestone.WorkItems)
            .Select(workItem => workItem.ContractWorkItemId)
            .ToList();
        if (workItemIds.Distinct().Count() != workItemIds.Count)
        {
            throw new BadRequestException("Work item IDs must be unique.");
        }
    }

    private static Milestone CreateMilestone(Milestone draft, Guid contractId, DateTime now)
    {
        return new Milestone
        {
            MilestonesId = draft.MilestonesId,
            ContractsId = contractId,
            Title = draft.Title,
            Description = draft.Description,
            Amount = draft.Amount,
            EstimatedDuration = draft.EstimatedDuration,
            DueDate = draft.DueDate,
            Deliverables = draft.Deliverables,
            AcceptanceCriteria = draft.AcceptanceCriteria,
            SortOrder = draft.SortOrder,
            Status = (int)MilestoneStatus.Pending,
            CreatedAt = now
        };
    }

    private static void ApplyMilestoneDraft(Milestone milestone, Milestone draft, DateTime now)
    {
        milestone.Title = draft.Title;
        milestone.Description = draft.Description;
        milestone.Amount = draft.Amount;
        milestone.EstimatedDuration = draft.EstimatedDuration;
        milestone.DueDate = draft.DueDate;
        milestone.Deliverables = draft.Deliverables;
        milestone.AcceptanceCriteria = draft.AcceptanceCriteria;
        milestone.SortOrder = draft.SortOrder;
        milestone.UpdatedAt = now;
    }

    private static ContractWorkItem CreateWorkItem(
        ContractWorkItem draft,
        Milestone milestone,
        DateTime now)
    {
        return new ContractWorkItem
        {
            ContractWorkItemId = draft.ContractWorkItemId,
            MilestonesId = milestone.MilestonesId,
            Milestone = milestone,
            Title = draft.Title,
            Description = draft.Description,
            Deliverables = draft.Deliverables,
            EstimatedDuration = draft.EstimatedDuration,
            OrderIndex = draft.OrderIndex,
            Status = (int)ContractWorkItemStatus.Todo,
            CreatedAt = now
        };
    }

    private static void ApplyWorkItemDraft(
        ContractWorkItem workItem,
        ContractWorkItem draft,
        Milestone milestone,
        DateTime now)
    {
        workItem.MilestonesId = milestone.MilestonesId;
        workItem.Milestone = milestone;
        workItem.Title = draft.Title;
        workItem.Description = draft.Description;
        workItem.Deliverables = draft.Deliverables;
        workItem.EstimatedDuration = draft.EstimatedDuration;
        workItem.OrderIndex = draft.OrderIndex;
        workItem.UpdatedAt = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
