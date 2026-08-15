using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.ESign;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.Accept.Commands;

public sealed class AcceptContractMilestonesCommandHandler :
    IRequestHandler<AcceptContractMilestonesCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService? _notificationService;

    public AcceptContractMilestonesCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService? notificationService = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<ContractWorkflowResponse> Handle(
        AcceptContractMilestonesCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingSignature)
        {
            throw new BadRequestException("Milestones can only be accepted while the contract is pending signatures.");
        }

        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateMilestonesForSubmitOrPublish(contract, milestones);

        var fullySignedDocument = await _context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contract.ContractsId &&
                document.Status == (int)ESignDocumentStatus.FullySigned)
            .OrderByDescending(document => document.FinalizedAt ?? document.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (fullySignedDocument is null)
        {
            throw new BadRequestException("Contract must be fully signed before accepting milestones.");
        }

        var now = _dateTimeService.UtcNow;
        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(escrow => escrow.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                RequiredAmount = contract.TotalBudget,
                FundedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.PendingFunding,
                CreatedAt = now
            };
            _context.Set<ContractEscrow>().Add(escrow);
        }
        else
        {
            escrow.RequiredAmount = contract.TotalBudget;
            escrow.FundedAmount = 0m;
            escrow.RequiredPercentage = 1.0m;
            escrow.Currency = string.IsNullOrWhiteSpace(escrow.Currency) ? "VND" : escrow.Currency;
            escrow.Status = (int)ContractEscrowStatus.PendingFunding;
            escrow.FundedAt = null;
            escrow.ReleasedAt = null;
        }

        contract.Status = (int)ContractStatus.PendingEscrow;
        contract.UpdatedAt = now;

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == contract.JobPostsId, cancellationToken);

        if (jobPost is not null)
        {
            jobPost.Status = 2; // Closed
            jobPost.UpdatedAt = now;
        }

        var conversations = await _context.Set<Conversation>()
            .Where(conversation => conversation.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        Guid? workspaceConversationId = null;
        foreach (var conversation in conversations)
        {
            if (conversation.ConversationType == (int)ConversationType.JobNegotiation)
            {
                conversation.ConversationType = (int)ConversationType.ContractWorkroom;
                conversation.UpdatedAt = now;
            }

            if (conversation.ConversationType == (int)ConversationType.ContractWorkroom)
            {
                workspaceConversationId ??= conversation.ConversationsId;
            }
        }

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Milestones accepted. Workspace is now open. Waiting for client escrow funding.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyWaitlistedFreelancers(contract, jobPost, cancellationToken);

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
                "ContractMilestonesAccepted",
                new { contractId = contract.ContractsId, conversationId = workspaceConversationId },
                cancellationToken);

            if (workspaceConversationId.HasValue)
            {
                await _chatRealtimeNotifier.SendUsersEventAsync(
                    [.. participantUserIds],
                    "WorkspaceOpened",
                    new { contractId = contract.ContractsId, conversationId = workspaceConversationId.Value },
                    cancellationToken);
            }
        }

        return new ContractWorkflowResponse(
            contract.ContractsId,
            contract.Status,
            escrow.ContractEscrowId,
            fullySignedDocument.EsignDocumentsId);
    }

    private async Task NotifyWaitlistedFreelancers(
        Contract contract,
        JobPost? jobPost,
        CancellationToken cancellationToken)
    {
        if (_notificationService is null)
        {
            return;
        }

        var waitlistedProposals = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.FreelancerProfiles)
            .Where(proposal =>
                proposal.JobPostsId == contract.JobPostsId &&
                proposal.ProposalsId != contract.ProposalsId &&
                (proposal.Status == 1 || proposal.Status == 2))
            .ToListAsync(cancellationToken);

        foreach (var proposal in waitlistedProposals)
        {
            await _notificationService.CreateNotificationAsync(
                proposal.FreelancerProfiles.UserId,
                NotificationType.ProposalStatusChanged,
                "Proposal moved to waiting list",
                $"The client selected another freelancer for \"{jobPost?.Title ?? contract.Title}\". Your proposal remains on the waiting list for future consideration.",
                proposal.ProposalsId,
                "Proposal",
                cancellationToken);
        }
    }
}
