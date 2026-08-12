using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Domain.Entities;
using Domain.Enums.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Negotiations.OpenFromInvite.Commands;

public class OpenNegotiationFromInviteCommandHandler
    : IRequestHandler<OpenNegotiationFromInviteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public OpenNegotiationFromInviteCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<Guid> Handle(
        OpenNegotiationFromInviteCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new ForbiddenAccessException("Only clients can invite freelancers.");
        }

        var jobPost = await _context.Set<JobPost>()
            .Include(item => item.JobPostMilestonePlans)
                .ThenInclude(item => item.WorkItems)
            .FirstOrDefaultAsync(
                jobPost => jobPost.JobPostsId == command.JobPostId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not own this job post.");
        }

        JobPostNegotiationGuard.EnsureEligibleForNegotiation(jobPost);

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == command.FreelancerProfileId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var freelancerConversationIds = await _context.Set<ConversationParticipant>()
            .Where(participant =>
                participant.UserId == freelancerProfile.UserId &&
                participant.LeftAt == null)
            .Select(participant => participant.ConversationsId)
            .ToListAsync(cancellationToken);

        var existingConversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                conversation =>
                    conversation.ConversationType == (int)ConversationType.JobNegotiation &&
                    conversation.JobPostsId == command.JobPostId &&
                    conversation.ProposalsId == null &&
                    freelancerConversationIds.Contains(conversation.ConversationsId) &&
                    conversation.DeletedAt == null,
                cancellationToken);

        if (existingConversation is not null)
        {
            await NotifyConversationUpdated(
                existingConversation.ConversationsId,
                existingConversation.LastMessageAt,
                cancellationToken);

            return existingConversation.ConversationsId;
        }

        var now = _dateTimeService.UtcNow;
        var conversation = new Conversation
        {
            ConversationsId = Guid.NewGuid(),
            ConversationType = (int)ConversationType.JobNegotiation,
            JobPostsId = command.JobPostId,
            ContractsId = null,
            CreatedByUserId = command.UserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = now
        };

        _context.Set<Conversation>().Add(conversation);
        _context.Set<ConversationParticipant>().Add(new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            UserId = clientProfile.UserId,
            ParticipantRole = (int)ParticipantRole.Client,
            JoinedAt = now
        });
        _context.Set<ConversationParticipant>().Add(new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            UserId = freelancerProfile.UserId,
            ParticipantRole = (int)ParticipantRole.Freelancer,
            JoinedAt = now
        });

        foreach (var plan in jobPost.JobPostMilestonePlans.OrderBy(item => item.OrderIndex))
        {
            var draft = new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = Guid.NewGuid(),
                ConversationsId = conversation.ConversationsId,
                Title = plan.Title,
                Description = plan.Description,
                Amount = plan.Amount,
                EstimatedDuration = plan.EstimatedDuration,
                DueDate = plan.DueDate,
                Deliverables = plan.Deliverables ?? string.Empty,
                AcceptanceCriteria = plan.AcceptanceCriteria ?? string.Empty,
                OrderIndex = plan.OrderIndex,
                CreatedAt = now
            };
            draft.WorkItems = plan.WorkItems.OrderBy(item => item.OrderIndex).Select((item, index) => new NegotiationMilestoneDraftWorkItem
            {
                NegotiationMilestoneDraftWorkItemId = Guid.NewGuid(),
                NegotiationMilestoneDraftId = draft.NegotiationMilestoneDraftId,
                Title = item.Title,
                Description = item.Description,
                Deliverables = item.Deliverables,
                EstimatedDuration = item.EstimatedDuration,
                OrderIndex = index
            }).ToList();
            _context.Set<NegotiationMilestoneDraft>().Add(draft);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await NotifyConversationUpdated(
            conversation.ConversationsId,
            conversation.LastMessageAt,
            cancellationToken);

        return conversation.ConversationsId;
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
