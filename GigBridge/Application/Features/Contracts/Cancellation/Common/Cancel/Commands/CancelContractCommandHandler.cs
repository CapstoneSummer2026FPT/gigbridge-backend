using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Contracts.Services;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Cancellation.Common.Cancel.Commands;

public sealed class CancelContractCommandHandler :
    IRequestHandler<CancelContractCommand, ContractWorkflowResponse>
{
    /// <summary>
    /// The self-service cancel button stays disabled until this much time has passed since
    /// <see cref="Contract.CreatedAt"/>. Enforced here, server-side, so refreshing or tampering
    /// with the client clock cannot unlock cancellation early.
    /// </summary>
    public static readonly TimeSpan CancellationWaitPeriod = TimeSpan.FromMinutes(1);

    private static readonly int[] CancellableStatuses =
    [
        (int)ContractStatus.PendingContractDetails,
        (int)ContractStatus.PendingContractConfirmation,
        (int)ContractStatus.PendingSignature
    ];

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly ILogger<CancelContractCommandHandler> _logger;

    public CancelContractCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notificationService,
        IUserAuditLogService userAuditLog,
        ILogger<CancelContractCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
        _userAuditLog = userAuditLog;
        _logger = logger;
    }

    public async Task<ContractWorkflowResponse> Handle(
        CancelContractCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.ClientProfilesId == contract.ClientProfilesId, cancellationToken)
            ?? throw new NotFoundException("Client profile does not exist.");

        var freelancerProfile = contract.FreelancerProfilesId.HasValue
            ? await _context.Set<FreelancerProfile>().FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken)
            : null;

        var cancelledByRole = ResolveCancellerRole(contract, clientProfile, freelancerProfile, command.UserId);

        if (contract.Status == (int)ContractStatus.Cancelled)
        {
            throw new ConflictException("This contract has already been cancelled.");
        }

        if (!CancellableStatuses.Contains(contract.Status))
        {
            throw new BadRequestException(
                "This contract can no longer be cancelled: signing has completed, escrow has been funded, or the contract has otherwise progressed.");
        }

        var now = _dateTimeService.UtcNow;
        var unlockAt = contract.CreatedAt.Add(CancellationWaitPeriod);
        if (now < unlockAt)
        {
            throw new BadRequestException(
                $"This contract cannot be cancelled until {unlockAt:O}.");
        }

        contract.Status = (int)ContractStatus.Cancelled;
        contract.CancelledAt = now;
        contract.CancelledByUserId = command.UserId;
        contract.UpdatedAt = now;

        if (freelancerProfile is not null)
        {
            await ServiceFeeWorkflow.RefundAsync(
                _context,
                freelancerProfile.UserId,
                contract.ContractsId,
                $"{ServiceFeeWorkflow.AcceptJobFeePrefix}{contract.ContractsId:N}",
                $"SERVICE-FEE-ACCEPT-REFUND-{contract.ContractsId:N}",
                "Freelancer acceptance service fee refunded: contract cancelled before signing completed.",
                now,
                cancellationToken);
        }

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Contract cancelled by the {cancelledByRole}.",
            now,
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            cancelledByRole,
            AuditUserActionType.ContractCancelled,
            contract.ContractsId,
            $"Contract cancelled by the {cancelledByRole} before signing completed.");

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            var participantUserIds = freelancerProfile is not null
                ? new[] { clientProfile.UserId, freelancerProfile.UserId }
                : new[] { clientProfile.UserId };

            var payload = new
            {
                contractId = contract.ContractsId,
                status = contract.Status,
                cancelledByRole = cancelledByRole.ToString(),
                cancelledAt = contract.CancelledAt
            };

            await _chatRealtimeNotifier.SendUsersEventAsync(
                participantUserIds,
                "ContractCancelled",
                payload,
                CancellationToken.None);

            foreach (var userId in participantUserIds)
            {
                await _notificationService.CreateNotificationAsync(
                    userId,
                    NotificationType.ContractCancelled,
                    "Contract cancelled",
                    $"The contract '{contract.Title}' was cancelled by the {cancelledByRole}.",
                    contract.ContractsId,
                    nameof(Contract),
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Post-cancel real-time/notification dispatch failed for contract {ContractId}; the cancellation itself was already committed.",
                contract.ContractsId);
        }

        return new ContractWorkflowResponse(
            contract.ContractsId,
            contract.Status,
            null,
            null);
    }

    private static UserRole ResolveCancellerRole(
        Contract contract,
        ClientProfile clientProfile,
        FreelancerProfile? freelancerProfile,
        Guid userId)
    {
        if (clientProfile.UserId == userId)
        {
            return UserRole.Client;
        }

        if (freelancerProfile is not null && freelancerProfile.UserId == userId)
        {
            return UserRole.Freelancer;
        }

        throw new ForbiddenAccessException("Only the client or the selected freelancer can cancel this contract.");
    }
}
