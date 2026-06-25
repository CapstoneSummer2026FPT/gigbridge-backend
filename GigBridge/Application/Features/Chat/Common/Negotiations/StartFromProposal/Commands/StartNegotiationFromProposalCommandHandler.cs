using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;

public class StartNegotiationFromProposalCommandHandler
    : IRequestHandler<StartNegotiationFromProposalCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public StartNegotiationFromProposalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<Guid> Handle(
        StartNegotiationFromProposalCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new ForbiddenAccessException("Only clients can start a negotiation.");
        }

        var proposal = await _context.Set<Proposal>()
            .Include(proposal => proposal.JobPosts)
            .FirstOrDefaultAsync(
                proposal => proposal.ProposalsId == command.ProposalId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        if (proposal.JobPosts.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not own this job post.");
        }

        if (proposal.JobPosts.Status != 1)
        {
            throw new BadRequestException("Job post is no longer open for negotiations.");
        }

        var now = _dateTimeService.UtcNow;
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(
                contract => contract.JobPostsId == proposal.JobPostsId,
                cancellationToken);

        if (contract is null)
        {
            contract = CreateDraftContract(proposal, now);
            _context.Set<Contract>().Add(contract);
        }

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == proposal.FreelancerProfilesId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var existingConversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                conversation =>
                    conversation.ConversationType == (int)ConversationType.JobNegotiation &&
                    conversation.JobPostsId == proposal.JobPostsId &&
                    conversation.ProposalsId == proposal.ProposalsId &&
                    conversation.DeletedAt == null,
                cancellationToken);

        if (existingConversation is not null)
        {
            await EnsureParticipants(
                existingConversation.ConversationsId,
                clientProfile.UserId,
                freelancerProfile.UserId,
                cancellationToken);

            if (existingConversation.ContractsId is null)
            {
                existingConversation.ContractsId = contract.ContractsId;
                existingConversation.UpdatedAt = now;
            }

            if (contract.Status == (int)ContractStatus.PendingFreelancerSelection ||
                contract.Status == (int)ContractStatus.Draft)
            {
                contract.Status = (int)ContractStatus.InNegotiation;
                contract.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await NotifyConversationUpdated(
                existingConversation.ConversationsId,
                existingConversation.LastMessageAt,
                cancellationToken);

            return existingConversation.ConversationsId;
        }

        var conversation = new Conversation
        {
            ConversationsId = Guid.NewGuid(),
            ConversationType = (int)ConversationType.JobNegotiation,
            JobPostsId = proposal.JobPostsId,
            ProposalsId = proposal.ProposalsId,
            ContractsId = contract.ContractsId,
            CreatedByUserId = command.UserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = now
        };

        _context.Set<Conversation>().Add(conversation);
        AddParticipant(conversation.ConversationsId, clientProfile.UserId, ParticipantRole.Client, now);
        AddParticipant(conversation.ConversationsId, freelancerProfile.UserId, ParticipantRole.Freelancer, now);

        contract.Status = (int)ContractStatus.InNegotiation;
        contract.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        await NotifyConversationUpdated(
            conversation.ConversationsId,
            conversation.LastMessageAt,
            cancellationToken);

        return conversation.ConversationsId;
    }

    private static Contract CreateDraftContract(Proposal proposal, DateTime now)
    {
        var jobPost = proposal.JobPosts;

        return new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = proposal.JobPostsId,
            ClientProfilesId = jobPost.ClientProfilesId,
            FreelancerProfilesId = null,
            ProposalsId = null,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = jobPost.BudgetMin ?? jobPost.BudgetMax ?? proposal.ProposedBudget ?? 0m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            EndDate = jobPost.EndDate.HasValue
                ? DateOnly.FromDateTime(jobPost.EndDate.Value)
                : null,
            CreatedAt = now
        };
    }

    private async Task EnsureParticipants(
        Guid conversationId,
        Guid clientUserId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var participants = await _context.Set<ConversationParticipant>()
            .Where(participant => participant.ConversationsId == conversationId)
            .ToListAsync(cancellationToken);
        var now = _dateTimeService.UtcNow;

        if (!participants.Any(participant => participant.UserId == clientUserId))
        {
            AddParticipant(conversationId, clientUserId, ParticipantRole.Client, now);
        }

        if (!participants.Any(participant => participant.UserId == freelancerUserId))
        {
            AddParticipant(conversationId, freelancerUserId, ParticipantRole.Freelancer, now);
        }
    }

    private void AddParticipant(
        Guid conversationId,
        Guid userId,
        ParticipantRole role,
        DateTime now)
    {
        _context.Set<ConversationParticipant>().Add(new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = conversationId,
            UserId = userId,
            ParticipantRole = (int)role,
            JoinedAt = now
        });
    }

    private async Task NotifyConversationUpdated(
        Guid conversationId,
        DateTime? lastMessageAt,
        CancellationToken cancellationToken)
    {
        var participants = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(participant =>
                participant.ConversationsId == conversationId &&
                participant.LeftAt == null &&
                participant.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants
            .GroupBy(participant => participant.UserId)
            .Select(group => group.First()))
        {
            await _chatRealtimeNotifier.SendUserEventAsync(
                participant.UserId,
                "ConversationUpdated",
                new
                {
                    conversationId,
                    lastMessage = (object?)null,
                    lastMessageAt,
                    unreadCount = participant.UnreadCount
                },
                cancellationToken);
        }
    }
}
