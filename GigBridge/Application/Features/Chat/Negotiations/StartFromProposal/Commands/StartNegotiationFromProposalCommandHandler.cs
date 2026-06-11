using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Negotiations.StartFromProposal.Commands;

public class StartNegotiationFromProposalCommandHandler
    : IRequestHandler<StartNegotiationFromProposalCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public StartNegotiationFromProposalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
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

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(
                contract => contract.JobPostsId == proposal.JobPostsId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract draft does not exist for this job post.");
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

            if (contract.Status == (int)ContractStatus.PendingFreelancerSelection ||
                contract.Status == (int)ContractStatus.Draft)
            {
                contract.Status = (int)ContractStatus.InNegotiation;
                contract.UpdatedAt = _dateTimeService.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return existingConversation.ConversationsId;
        }

        var now = _dateTimeService.UtcNow;
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

        return conversation.ConversationsId;
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
}
