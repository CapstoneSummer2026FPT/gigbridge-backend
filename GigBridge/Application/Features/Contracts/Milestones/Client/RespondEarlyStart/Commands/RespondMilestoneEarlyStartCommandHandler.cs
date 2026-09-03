using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts.Milestones;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Client.RespondEarlyStart.Commands;

public sealed class RespondMilestoneEarlyStartCommandHandler
    : IRequestHandler<RespondMilestoneEarlyStartCommand, MilestoneEarlyStartRequestDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public RespondMilestoneEarlyStartCommandHandler(
        IApplicationDbContext context, IDateTimeService clock, IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _clock = clock;
        _realtimeNotifier = realtimeNotifier;
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

        if (milestone.Status != (int)MilestoneStatus.Pending)
        {
            MilestoneEarlyStartRequestWorkflow.CancelAsSuperseded(request, now);
            contract.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            return ToResponse(request);
        }

        if (command.Request.Approve)
        {
            var activeCount = await _context.Set<Milestone>().CountAsync(
                item => item.ContractsId == command.ContractId && item.Status == (int)MilestoneStatus.InProgress,
                cancellationToken);
            if (activeCount >= 2) throw new BadRequestException("At most two milestones may be in progress at the same time.");
            milestone.Status = (int)MilestoneStatus.InProgress;
            milestone.StartedAt = now;
            milestone.UpdatedAt = now;
        }

        request.Status = command.Request.Approve
            ? (int)MilestoneEarlyStartRequestStatus.Approved
            : (int)MilestoneEarlyStartRequestStatus.Rejected;
        request.ResponseNote = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();
        request.RespondedByUserId = command.UserId;
        request.RespondedAt = now;
        contract.UpdatedAt = now;

        var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            command.Request.Approve
                ? $"Client approved early start for milestone: {milestone.Title}."
                : $"Client rejected early start for milestone: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier is not null)
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(_context, contract, cancellationToken);
            await _realtimeNotifier.SendUsersEventAsync(
                participantIds,
                "EarlyStartResponded",
                new
                {
                    contractId = contract.ContractsId,
                    milestoneId = milestone.MilestonesId,
                    requestId = request.MilestoneEarlyStartRequestId,
                    approved = command.Request.Approve,
                    milestoneStatus = milestone.Status
                },
                cancellationToken);

            if (systemMessage is not null)
            {
                var messagePayload = ContractConversationEvents.ToRealtimePayload(systemMessage);
                await _realtimeNotifier.SendUsersEventAsync(participantIds, "ReceiveMessage", messagePayload, cancellationToken);
                await _realtimeNotifier.SendConversationEventAsync(systemMessage.ConversationsId, "ReceiveMessage", messagePayload, cancellationToken);
            }
        }

        return ToResponse(request);
    }

    private static MilestoneEarlyStartRequestDto ToResponse(MilestoneEarlyStartRequest request) =>
        new(
            request.MilestoneEarlyStartRequestId,
            request.ContractsId,
            request.MilestonesId,
            request.Reason,
            request.ResponseNote,
            request.Status,
            request.CreatedAt,
            request.RespondedAt);
}
