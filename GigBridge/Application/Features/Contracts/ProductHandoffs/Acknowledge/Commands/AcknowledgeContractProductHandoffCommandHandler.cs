using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.Acknowledge.Commands;

public sealed class AcknowledgeContractProductHandoffCommandHandler :
    IRequestHandler<AcknowledgeContractProductHandoffCommand, ContractProductHandoffResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public AcknowledgeContractProductHandoffCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractProductHandoffResponse> Handle(
        AcknowledgeContractProductHandoffCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await ContractProductHandoffAccess.GetActiveContractAsync(
            _context,
            command.ContractId,
            cancellationToken);

        await ContractProductHandoffAccess.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        var handoff = await _context.Set<ContractProductHandoff>()
            .FirstOrDefaultAsync(
                handoff =>
                    handoff.ContractProductHandoffId == command.HandoffId &&
                    handoff.ContractsId == command.ContractId,
                cancellationToken);

        if (handoff is null)
        {
            throw new NotFoundException("Product handoff does not exist.");
        }

        if (handoff.ReceivedByUserId == command.UserId && handoff.ReceivedAt.HasValue)
        {
            return ContractProductHandoffMapper.ToResponse(handoff);
        }

        if (handoff.ReceivedByUserId.HasValue && handoff.ReceivedByUserId != command.UserId)
        {
            throw new ConflictException("Product handoff has already been acknowledged.");
        }

        var now = _dateTimeService.UtcNow;
        handoff.ReceivedByUserId = command.UserId;
        handoff.ReceivedAt = now;
        contract.UpdatedAt = now;

        var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Freelancer received product materials.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var participantUserIds = await ContractProductHandoffAccess.GetParticipantUserIdsAsync(
            _context,
            contract,
            cancellationToken);

        if (participantUserIds.Count > 0)
        {
            await _chatRealtimeNotifier.SendUsersEventAsync(
                participantUserIds,
                "ProductHandoffAcknowledged",
                ContractProductHandoffMapper.ToResponse(handoff),
                cancellationToken);
        }

        if (systemMessage is not null)
        {
            var messagePayload = ContractConversationEvents.ToRealtimePayload(systemMessage);

            if (participantUserIds.Count > 0)
            {
                await _chatRealtimeNotifier.SendUsersEventAsync(
                    participantUserIds,
                    "ReceiveMessage",
                    messagePayload,
                    cancellationToken);
            }

            await _chatRealtimeNotifier.SendConversationEventAsync(
                systemMessage.ConversationsId,
                "ReceiveMessage",
                messagePayload,
                cancellationToken);
        }

        return ContractProductHandoffMapper.ToResponse(handoff);
    }
}
