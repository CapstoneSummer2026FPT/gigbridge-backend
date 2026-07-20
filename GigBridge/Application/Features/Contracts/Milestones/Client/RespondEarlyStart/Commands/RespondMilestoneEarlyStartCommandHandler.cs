using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Client.RespondEarlyStart.Commands;

public sealed class RespondMilestoneEarlyStartCommandHandler
    : IRequestHandler<RespondMilestoneEarlyStartCommand, MilestoneEarlyStartRequestDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    public RespondMilestoneEarlyStartCommandHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<MilestoneEarlyStartRequestDto> Handle(RespondMilestoneEarlyStartCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);
        var request = await _context.Set<MilestoneEarlyStartRequest>()
            .FirstOrDefaultAsync(item => item.MilestoneEarlyStartRequestId == command.RequestId && item.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Early start request does not exist.");
        if (request.Status != (int)MilestoneEarlyStartRequestStatus.Pending)
            throw new BadRequestException("Only pending early start requests can be answered.");

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(_context, command.ContractId, request.MilestonesId, cancellationToken);
        var now = _clock.UtcNow;
        request.Status = command.Request.Approve
            ? (int)MilestoneEarlyStartRequestStatus.Approved
            : (int)MilestoneEarlyStartRequestStatus.Rejected;
        request.ResponseNote = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();
        request.RespondedByUserId = command.UserId;
        request.RespondedAt = now;
        if (command.Request.Approve)
        {
            var activeCount = await _context.Set<Milestone>().CountAsync(
                item => item.ContractsId == command.ContractId && item.Status == (int)MilestoneStatus.InProgress,
                cancellationToken);
            if (activeCount >= 2) throw new BadRequestException("At most two milestones may be in progress at the same time.");
            if (milestone.Status != (int)MilestoneStatus.Pending) throw new BadRequestException("Milestone is no longer pending.");
            milestone.Status = (int)MilestoneStatus.InProgress;
            milestone.StartedAt = now;
            milestone.UpdatedAt = now;
        }
        contract.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return new MilestoneEarlyStartRequestDto(request.MilestoneEarlyStartRequestId, request.ContractsId, request.MilestonesId,
            request.Reason, request.ResponseNote, request.Status, request.CreatedAt, request.RespondedAt);
    }
}
