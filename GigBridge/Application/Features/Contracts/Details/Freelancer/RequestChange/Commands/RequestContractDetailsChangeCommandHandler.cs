using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
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

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractDetails;
        contract.UpdatedAt = now;

        var message = string.IsNullOrWhiteSpace(command.Request.Reason)
            ? "Contract details change requested."
            : $"Contract details change requested: {command.Request.Reason}";

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
