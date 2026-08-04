using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Amendments.Common;
using Application.Features.Contracts.Amendments.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Contracts.Amendments.Commands;

public sealed record CreateContractAmendmentCommand(Guid ContractId, Guid UserId, CreateContractAmendmentRequest Request) : IRequest<Guid>;
public sealed record UpdateContractAmendmentCommand(Guid ContractId, Guid AmendmentId, Guid UserId, CreateContractAmendmentRequest Request) : IRequest<bool>;
public sealed record RespondContractAmendmentCommand(Guid ContractId, Guid AmendmentId, Guid UserId, RespondContractAmendmentRequest Request) : IRequest<bool>;
public sealed record SignContractAmendmentCommand(Guid ContractId, Guid AmendmentId, Guid UserId, SignContractAmendmentRequest Request) : IRequest<bool>;
public sealed record FundContractAmendmentCommand(Guid ContractId, Guid AmendmentId, Guid UserId) : IRequest<bool>;

public sealed class CreateContractAmendmentCommandHandler : IRequestHandler<CreateContractAmendmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public CreateContractAmendmentCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }

    public async Task<Guid> Handle(CreateContractAmendmentCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);
        var changeRequest = await _context.Set<ContractChangeRequest>()
            .SingleOrDefaultAsync(item => item.ContractChangeRequestId == command.Request.ChangeRequestId && item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Accepted change request does not exist.");
        if (changeRequest.Status != (int)ContractChangeRequestStatus.Accepted)
            throw new BadRequestException("An amendment can only be created from an accepted change request.");
        if (await _context.Set<ContractAmendment>().AnyAsync(item => item.ContractChangeRequestId == changeRequest.ContractChangeRequestId, cancellationToken))
            throw new BadRequestException("This change request already has an amendment.");

        var amendment = new ContractAmendment
        {
            ContractAmendmentId = Guid.NewGuid(), ContractsId = contract.ContractsId,
            ContractChangeRequestId = changeRequest.ContractChangeRequestId, CreatedByUserId = command.UserId,
            RevisionNumber = contract.RevisionNumber + 1, Reason = command.Request.Reason.Trim(),
            OriginalTotalBudget = contract.TotalBudget, Status = (int)ContractAmendmentStatus.PendingFreelancerReview,
            CreatedAt = _clock.UtcNow
        };
        await ReplaceSnapshotAsync(_context, contract, amendment, command.Request, _clock.UtcNow, cancellationToken);
        _context.Set<ContractAmendment>().Add(amendment);
        await _context.SaveChangesAsync(cancellationToken);
        return amendment.ContractAmendmentId;
    }

    internal static async Task ReplaceSnapshotAsync(
        IApplicationDbContext context, Contract contract, ContractAmendment amendment,
        CreateContractAmendmentRequest request, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BadRequestException("Amendment reason is required.");
        if (request.Milestones.Count == 0) throw new BadRequestException("At least one future milestone is required.");
        if (request.Milestones.Select(item => item.SortOrder).Any(item => !item.HasValue) ||
            request.Milestones.GroupBy(item => item.SortOrder!.Value).Any(group => group.Count() > 1))
            throw new BadRequestException("Future milestone order must be present and unique.");

        var existing = await context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);
        var pendingIds = existing.Where(item => item.Status == (int)MilestoneStatus.Pending).Select(item => item.MilestonesId).ToHashSet();
        var requestedSourceIds = request.Milestones.Where(item => item.MilestoneId.HasValue).Select(item => item.MilestoneId!.Value).ToList();
        if (requestedSourceIds.Distinct().Count() != requestedSourceIds.Count || requestedSourceIds.Any(item => !pendingIds.Contains(item)))
            throw new BadRequestException("Only pending milestones may be changed by an amendment.");

        var lockedAmount = existing.Where(item => item.Status != (int)MilestoneStatus.Pending).Sum(item => item.Amount);
        amendment.Milestones.Clear();
        foreach (var item in request.Milestones.OrderBy(item => item.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Amount <= 0 || string.IsNullOrWhiteSpace(item.Deliverables) || string.IsNullOrWhiteSpace(item.AcceptanceCriteria))
                throw new BadRequestException("Each amendment milestone requires title, positive amount, deliverables and acceptance criteria.");
            if (item.DueDate.HasValue && item.DueDate.Value < DateOnly.FromDateTime(now))
                throw new BadRequestException("Amendment milestone deadline cannot be in the past.");
            if (item.WorkItems is null || item.WorkItems.Count == 0)
                throw new BadRequestException("Each amendment milestone requires at least one work item.");
            if (item.WorkItems.GroupBy(workItem => workItem.OrderIndex).Any(group => group.Count() > 1))
                throw new BadRequestException("Work item order must be unique within each milestone.");

            var snapshot = new ContractAmendmentMilestone
            {
                ContractAmendmentMilestoneId = Guid.NewGuid(), SourceMilestoneId = item.MilestoneId,
                Title = item.Title.Trim(), Description = item.Description?.Trim(), Amount = item.Amount,
                EstimatedDuration = item.EstimatedDuration?.Trim(), DueDate = item.DueDate,
                Deliverables = item.Deliverables.Trim(), AcceptanceCriteria = item.AcceptanceCriteria.Trim(),
                OrderIndex = item.SortOrder!.Value
            };
            foreach (var workItem in item.WorkItems.OrderBy(workItem => workItem.OrderIndex))
            {
                if (string.IsNullOrWhiteSpace(workItem.Title) || string.IsNullOrWhiteSpace(workItem.Description))
                    throw new BadRequestException("Each amendment work item requires title and description.");
                snapshot.WorkItems.Add(new ContractAmendmentWorkItem
                {
                    ContractAmendmentWorkItemId = Guid.NewGuid(), SourceContractWorkItemId = workItem.WorkItemId,
                    Title = workItem.Title.Trim(), Description = workItem.Description.Trim(),
                    Deliverables = workItem.Deliverables?.Trim(), EstimatedDuration = workItem.EstimatedDuration?.Trim(),
                    OrderIndex = workItem.OrderIndex
                });
            }
            amendment.Milestones.Add(snapshot);
        }

        amendment.Reason = request.Reason.Trim();
        amendment.ProposedTotalBudget = lockedAmount + amendment.Milestones.Sum(item => item.Amount);
        amendment.BudgetDelta = amendment.ProposedTotalBudget - contract.TotalBudget;
        if (amendment.ProposedTotalBudget < lockedAmount)
            throw new BadRequestException("Amendment budget cannot be lower than released and locked milestone amounts.");
    }
}

public sealed class UpdateContractAmendmentCommandHandler : IRequestHandler<UpdateContractAmendmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public UpdateContractAmendmentCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }
    public async Task<bool> Handle(UpdateContractAmendmentCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);
        var amendment = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .Include(item => item.Signatures)
            .SingleOrDefaultAsync(item => item.ContractAmendmentId == command.AmendmentId && item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract amendment does not exist.");
        if (amendment.Status is not ((int)ContractAmendmentStatus.PendingFreelancerReview) and not ((int)ContractAmendmentStatus.ChangeRequested))
            throw new BadRequestException("This amendment can no longer be edited.");
        _context.Set<ContractAmendmentWorkItem>().RemoveRange(amendment.Milestones.SelectMany(item => item.WorkItems));
        _context.Set<ContractAmendmentMilestone>().RemoveRange(amendment.Milestones);
        amendment.Milestones.Clear();
        _context.Set<ContractAmendmentSignature>().RemoveRange(amendment.Signatures);
        amendment.Signatures.Clear();
        await CreateContractAmendmentCommandHandler.ReplaceSnapshotAsync(_context, contract, amendment, command.Request, _clock.UtcNow, cancellationToken);
        amendment.ReviewNote = null;
        amendment.DocumentSnapshotJson = null;
        amendment.Status = (int)ContractAmendmentStatus.PendingFreelancerReview;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class RespondContractAmendmentCommandHandler : IRequestHandler<RespondContractAmendmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public RespondContractAmendmentCommandHandler(IApplicationDbContext context) { _context = context; }
    public async Task<bool> Handle(RespondContractAmendmentCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);
        var amendment = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .SingleOrDefaultAsync(
            item => item.ContractAmendmentId == command.AmendmentId && item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract amendment does not exist.");
        if (amendment.Status != (int)ContractAmendmentStatus.PendingFreelancerReview)
            throw new BadRequestException("This amendment is not awaiting freelancer review.");
        if (!command.Request.Accept && !command.Request.RequestChanges)
        {
            amendment.ReviewNote = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();
            amendment.Status = (int)ContractAmendmentStatus.Rejected;
        }
        else if (command.Request.RequestChanges)
        {
            if (string.IsNullOrWhiteSpace(command.Request.Note)) throw new BadRequestException("A reason is required when requesting amendment changes.");
            amendment.ReviewNote = command.Request.Note.Trim();
            amendment.Status = (int)ContractAmendmentStatus.ChangeRequested;
        }
        else
        {
            amendment.ReviewNote = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();
            amendment.DocumentSnapshotJson = JsonSerializer.Serialize(new
            {
                amendment.ContractAmendmentId,
                amendment.ContractsId,
                amendment.RevisionNumber,
                amendment.Reason,
                amendment.OriginalTotalBudget,
                amendment.ProposedTotalBudget,
                amendment.BudgetDelta,
                Milestones = amendment.Milestones.OrderBy(item => item.OrderIndex).Select(item => new
                {
                    item.SourceMilestoneId,
                    item.Title,
                    item.Description,
                    item.Amount,
                    item.EstimatedDuration,
                    item.DueDate,
                    item.Deliverables,
                    item.AcceptanceCriteria,
                    item.OrderIndex,
                    WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select(workItem => new
                    {
                        workItem.SourceContractWorkItemId,
                        workItem.Title,
                        workItem.Description,
                        workItem.Deliverables,
                        workItem.EstimatedDuration,
                        workItem.OrderIndex
                    })
                })
            });
            amendment.Status = (int)ContractAmendmentStatus.PendingSignatures;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class SignContractAmendmentCommandHandler : IRequestHandler<SignContractAmendmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public SignContractAmendmentCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }
    public async Task<bool> Handle(SignContractAmendmentCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.SignatureData)) throw new BadRequestException("Signature is required.");
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        var role = await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, command.UserId, cancellationToken);
        var amendment = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .Include(item => item.Signatures)
            .SingleOrDefaultAsync(item => item.ContractAmendmentId == command.AmendmentId && item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract amendment does not exist.");
        if (amendment.Status != (int)ContractAmendmentStatus.PendingSignatures)
            throw new BadRequestException("This amendment is not ready for signatures.");
        if (string.IsNullOrWhiteSpace(amendment.DocumentSnapshotJson))
            throw new BadRequestException("The immutable amendment document is missing.");
        if (amendment.Signatures.Any(item => item.UserId == command.UserId)) return true;
        amendment.Signatures.Add(new ContractAmendmentSignature
        {
            ContractAmendmentSignatureId = Guid.NewGuid(), UserId = command.UserId,
            SignerRole = (int)role, SignatureData = command.Request.SignatureData,
            SignedAt = _clock.UtcNow
        });
        if (amendment.Signatures.Select(item => item.UserId).Distinct().Count() == 2)
        {
            if (amendment.BudgetDelta > 0) amendment.Status = (int)ContractAmendmentStatus.PendingFunding;
            else
            {
                await ContractAmendmentWorkflow.RefundDecreaseAsync(_context, _clock, contract, amendment, cancellationToken);
                await ContractAmendmentWorkflow.ApplyAsync(_context, _clock, contract, amendment, cancellationToken);
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

public sealed class FundContractAmendmentCommandHandler : IRequestHandler<FundContractAmendmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public FundContractAmendmentCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }
    public async Task<bool> Handle(FundContractAmendmentCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);
        var amendment = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .SingleOrDefaultAsync(item => item.ContractAmendmentId == command.AmendmentId && item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract amendment does not exist.");
        if (amendment.Status != (int)ContractAmendmentStatus.PendingFunding)
            throw new BadRequestException("This amendment is not awaiting additional funding.");
        await ContractAmendmentWorkflow.FundIncreaseAsync(_context, _clock, contract, amendment, command.UserId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
