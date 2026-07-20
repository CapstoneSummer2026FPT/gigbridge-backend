using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Amendments.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Amendments.Commands;

public sealed record CreateContractChangeRequestCommand(Guid ContractId, Guid UserId, CreateContractChangeRequest Request) : IRequest<Guid>;
public sealed record RespondContractChangeRequestCommand(Guid ContractId, Guid RequestId, Guid UserId, RespondContractChangeRequest Request) : IRequest<bool>;

public sealed class CreateContractChangeRequestCommandHandler : IRequestHandler<CreateContractChangeRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public CreateContractChangeRequestCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }

    public async Task<Guid> Handle(CreateContractChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, command.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(command.Request.Reason) || string.IsNullOrWhiteSpace(command.Request.RequestedChanges))
            throw new BadRequestException("Change request reason and requested changes are required.");

        var pendingMilestoneIds = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == command.ContractId && item.Status == (int)MilestoneStatus.Pending)
            .Select(item => item.MilestonesId)
            .ToListAsync(cancellationToken);
        if (command.Request.AffectedMilestoneIds.Except(pendingMilestoneIds).Any())
            throw new BadRequestException("Only pending milestones may be included in a change request.");
        var validWorkItems = await _context.Set<ContractWorkItem>()
            .Where(item => pendingMilestoneIds.Contains(item.MilestonesId))
            .Select(item => item.ContractWorkItemId)
            .ToListAsync(cancellationToken);
        if (command.Request.AffectedWorkItemIds.Except(validWorkItems).Any())
            throw new BadRequestException("Only work items from pending milestones may be changed.");

        var request = new ContractChangeRequest
        {
            ContractChangeRequestId = Guid.NewGuid(), ContractsId = command.ContractId,
            RequestedByUserId = command.UserId, Reason = command.Request.Reason.Trim(),
            RequestedChanges = command.Request.RequestedChanges.Trim(),
            AffectedMilestoneIds = command.Request.AffectedMilestoneIds.Distinct().ToArray(),
            AffectedWorkItemIds = command.Request.AffectedWorkItemIds.Distinct().ToArray(),
            Status = (int)ContractChangeRequestStatus.Pending, CreatedAt = _clock.UtcNow
        };
        _context.Set<ContractChangeRequest>().Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request.ContractChangeRequestId;
    }
}

public sealed class RespondContractChangeRequestCommandHandler : IRequestHandler<RespondContractChangeRequestCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public RespondContractChangeRequestCommandHandler(IApplicationDbContext context, IDateTimeService clock) { _context = context; _clock = clock; }

    public async Task<bool> Handle(RespondContractChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, command.UserId, cancellationToken);
        var request = await _context.Set<ContractChangeRequest>().FirstOrDefaultAsync(
            item => item.ContractChangeRequestId == command.RequestId && item.ContractsId == command.ContractId,
            cancellationToken) ?? throw new NotFoundException("Contract change request does not exist.");
        if (request.Status == (int)ContractChangeRequestStatus.NeedsClarification)
        {
            if (request.RequestedByUserId != command.UserId)
                throw new ForbiddenAccessException("Only the requester can provide the requested clarification.");
            if (string.IsNullOrWhiteSpace(command.Request.Note))
                throw new BadRequestException("Clarification is required.");

            request.ClarificationResponseNote = command.Request.Note.Trim();
            request.ClarifiedAt = _clock.UtcNow;
            request.Status = (int)ContractChangeRequestStatus.Pending;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (request.RequestedByUserId == command.UserId)
            throw new ForbiddenAccessException("The requester cannot answer their own change request.");
        if (request.Status != (int)ContractChangeRequestStatus.Pending)
            throw new BadRequestException("Change request has already been answered.");
        if (command.Request.NeedsClarification && string.IsNullOrWhiteSpace(command.Request.Note))
            throw new BadRequestException("A clarification question is required.");

        request.Status = command.Request.NeedsClarification
            ? (int)ContractChangeRequestStatus.NeedsClarification
            : command.Request.Accept ? (int)ContractChangeRequestStatus.Accepted : (int)ContractChangeRequestStatus.Rejected;
        if (command.Request.NeedsClarification)
        {
            request.ClarificationRequestNote = command.Request.Note!.Trim();
            request.ClarificationResponseNote = null;
            request.ClarifiedAt = null;
        }
        else
        {
            request.ResponseNote = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();
        }
        request.RespondedByUserId = command.UserId;
        request.RespondedAt = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
