using Application.Common.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Common.Internal;

/// <summary>
/// Pushes the real-time events that announce a funded escrow: the contract has become Active
/// and the negotiation conversation has turned into the contract workspace.
///
/// Every event is sent per-user rather than to the conversation group, because the parties are
/// typically sitting on the contract detail page, which never invokes ChatHub.JoinConversation.
/// Group-only delivery is why the freelancer's "waiting for escrow funding" card used to stay
/// stale until a manual refresh.
/// </summary>
internal static class ContractEscrowRealtimeEvents
{
    public const string EscrowFunded = "EscrowFunded";
    public const string WorkspaceOpened = "WorkspaceOpened";
    public const string ConversationUpdated = "ConversationUpdated";
    public const string ReceiveMessage = "ReceiveMessage";

    /// <summary>
    /// Announces a completed escrow funding to both parties. Call this after the funding has
    /// been committed: delivery failures are logged and swallowed so a dropped SignalR frame
    /// can never roll back money that already moved.
    /// </summary>
    public static async Task PublishEscrowFundedAsync(
        IApplicationDbContext context,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger logger,
        Contract contract,
        Guid? workspaceConversationId,
        int escrowStatus,
        Message? systemMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var participantUserIds = await context.Set<ConversationParticipant>()
                .AsNoTracking()
                .Where(participant =>
                    participant.Conversations.ContractsId == contract.ContractsId &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null)
                .Select(participant => participant.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (participantUserIds.Count == 0)
            {
                return;
            }

            Guid[] recipients = [.. participantUserIds];

            // The contract detail page keys off this event specifically. WorkspaceOpened alone
            // is not enough: AcceptContractMilestonesCommandHandler already emits that while the
            // contract is still PendingEscrow, which would refetch at the wrong moment.
            await chatRealtimeNotifier.SendUsersEventAsync(
                recipients,
                EscrowFunded,
                new
                {
                    contractId = contract.ContractsId,
                    conversationId = workspaceConversationId,
                    contractStatus = contract.Status,
                    escrowStatus
                },
                cancellationToken);

            if (workspaceConversationId.HasValue)
            {
                await chatRealtimeNotifier.SendUsersEventAsync(
                    recipients,
                    WorkspaceOpened,
                    new
                    {
                        contractId = contract.ContractsId,
                        conversationId = workspaceConversationId.Value
                    },
                    cancellationToken);

                // The conversation just moved from JobNegotiation to ContractWorkroom, so the
                // inbox has to re-categorise it.
                await chatRealtimeNotifier.SendUsersEventAsync(
                    recipients,
                    ConversationUpdated,
                    new
                    {
                        contractId = contract.ContractsId,
                        conversationId = workspaceConversationId.Value
                    },
                    cancellationToken);
            }

            if (systemMessage is not null)
            {
                var messagePayload = ContractConversationEvents.ToRealtimePayload(systemMessage);

                await chatRealtimeNotifier.SendUsersEventAsync(
                    recipients,
                    ReceiveMessage,
                    messagePayload,
                    cancellationToken);

                await chatRealtimeNotifier.SendConversationEventAsync(
                    systemMessage.ConversationsId,
                    ReceiveMessage,
                    messagePayload,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Post-funding real-time notification failed for contract {ContractId}; the escrow funding itself was already committed.",
                contract.ContractsId);
        }
    }
}
