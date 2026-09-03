using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Scheduling;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Negotiations.Realtime;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common;
using Domain.Entities;
using Domain.Enums.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.FinalOffers.Create.Commands;

public class CreateFinalOfferCommandHandler : IRequestHandler<CreateFinalOfferCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public CreateFinalOfferCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<Guid> Handle(
        CreateFinalOfferCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                conversation => conversation.ConversationsId == request.ConversationId,
                cancellationToken);

        if (conversation is null)
        {
            throw new NotFoundException("Conversation does not exist.");
        }

        if (conversation.ConversationType != (int)ConversationType.JobNegotiation)
        {
            throw new BadRequestException("Final offers can only be created in job negotiation conversations.");
        }

        if (!conversation.JobPostsId.HasValue)
        {
            throw new BadRequestException("Job negotiation conversation must be attached to a job post.");
        }

        var clientParticipant = await _context.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == command.UserId &&
                    participant.LeftAt == null,
                cancellationToken);

        if (clientParticipant is null ||
            clientParticipant.ParticipantRole != (int)ParticipantRole.Client)
        {
            throw new ForbiddenAccessException("Only the client participant can create a final offer.");
        }

        await JobPostNegotiationGuard.EnsureEligibleForNegotiationAsync(
            _context,
            conversation.JobPostsId.Value,
            cancellationToken);

        ValidateRequest(request.FinalPrice, request.StartDate, request.EndDate, request.Milestones);

        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.JobPostsId == conversation.JobPostsId.Value, cancellationToken);
        if (jobPost is null || clientProfile is null || clientProfile.ClientProfilesId != jobPost.ClientProfilesId)
        {
            throw new ForbiddenAccessException("Only the owning client can create a final offer.");
        }

        var freelancerParticipant = await _context.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.ParticipantRole == (int)ParticipantRole.Freelancer &&
                    participant.LeftAt == null,
                cancellationToken);

        if (freelancerParticipant is null)
        {
            throw new BadRequestException("Conversation does not have a freelancer participant.");
        }

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == freelancerParticipant.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var pendingOffers = await _context.Set<NegotiationOffer>()
            .Where(offer =>
                offer.ConversationsId == request.ConversationId &&
                offer.Status == (int)NegotiationOfferStatus.PendingFreelancerConfirmation)
            .ToListAsync(cancellationToken);

        foreach (var pendingOffer in pendingOffers)
        {
            pendingOffer.Status = (int)NegotiationOfferStatus.Cancelled;
            pendingOffer.RespondedAt = _dateTimeService.UtcNow;
        }

        var now = _dateTimeService.UtcNow;
        var offer = new NegotiationOffer
        {
            NegotiationOfferId = Guid.NewGuid(),
            ConversationsId = request.ConversationId,
            JobPostsId = conversation.JobPostsId.Value,
            ContractsId = null,
            ProposalsId = conversation.ProposalsId,
            ClientProfilesId = clientProfile.ClientProfilesId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            FinalPrice = request.FinalPrice,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ScopeSummary = request.ScopeSummary,
            ClientNote = request.ClientNote,
            Status = (int)NegotiationOfferStatus.PendingFreelancerConfirmation,
            CreatedAt = now
        };

        _context.Set<NegotiationOffer>().Add(offer);

        var existingDrafts = await _context.Set<NegotiationMilestoneDraft>()
            .Where(item => item.ConversationsId == request.ConversationId)
            .ToListAsync(cancellationToken);
        _context.Set<NegotiationMilestoneDraft>().RemoveRange(existingDrafts);

        var orderedMilestones = request.Milestones!.OrderBy(item => item.OrderIndex).ToList();
        var computedDueDates = MilestoneDeadlineCalculator.CalculateDueDates(
            DateOnly.FromDateTime(_dateTimeService.UtcNow),
            orderedMilestones.Select(item => item.EstimatedDuration).ToList());

        foreach (var (milestone, milestoneIndex) in orderedMilestones.Select((item, index) => (item, index)))
        {
            var dueDate = computedDueDates[milestoneIndex];
            var draftId = Guid.NewGuid();
            _context.Set<NegotiationMilestoneDraft>().Add(new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = draftId,
                ConversationsId = request.ConversationId,
                Title = milestone.Title!.Trim(),
                Description = Clean(milestone.Description),
                Amount = milestone.Amount,
                EstimatedDuration = Clean(milestone.EstimatedDuration),
                DueDate = dueDate,
                Deliverables = milestone.Deliverables!.Trim(),
                AcceptanceCriteria = milestone.AcceptanceCriteria!.Trim(),
                OrderIndex = milestone.OrderIndex,
                CreatedAt = now,
                UpdatedAt = now
            });
            var snapshot = new NegotiationOfferMilestone
            {
                NegotiationOfferMilestoneId = Guid.NewGuid(),
                NegotiationOfferId = offer.NegotiationOfferId,
                Title = milestone.Title.Trim(),
                Description = Clean(milestone.Description),
                Amount = milestone.Amount,
                EstimatedDuration = Clean(milestone.EstimatedDuration),
                DueDate = dueDate,
                Deliverables = milestone.Deliverables.Trim(),
                AcceptanceCriteria = milestone.AcceptanceCriteria.Trim(),
                OrderIndex = milestone.OrderIndex
            };
            snapshot.WorkItems = milestone.WorkItems.OrderBy(item => item.OrderIndex).Select((item, index) => new NegotiationOfferWorkItem
            {
                NegotiationOfferWorkItemId = Guid.NewGuid(),
                NegotiationOfferMilestoneId = snapshot.NegotiationOfferMilestoneId,
                Title = item.Title!.Trim(),
                Description = Clean(item.Description),
                Deliverables = Clean(item.Deliverables),
                EstimatedDuration = Clean(item.EstimatedDuration),
                OrderIndex = index
            }).ToList();
            _context.Set<NegotiationOfferMilestone>().Add(snapshot);
        }

        var message = AddConversationMessage(
            conversation,
            command.UserId,
            MessageType.FinalOffer,
            offer.FinalPrice.ToString("F0"),
            JsonSerializer.Serialize(new { negotiationOfferId = offer.NegotiationOfferId }),
            now);

        IncrementUnreadCounts(conversation.ConversationsId, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var activeParticipants = await GetActiveParticipants(
            conversation.ConversationsId,
            cancellationToken);
        var participantUserIds = activeParticipants
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArray();
        var messageResponse = ToMessageResponse(message);

        await _chatRealtimeNotifier.SendUsersEventAsync(
            participantUserIds,
            "ReceiveMessage",
            messageResponse,
            cancellationToken);

        await _chatRealtimeNotifier.SendUsersEventAsync(
            participantUserIds,
            NegotiationRealtimeEvents.FinalOfferCreated,
            new
            {
                conversationId = conversation.ConversationsId,
                offerId = offer.NegotiationOfferId,
                messageId = message.MessagesId
            },
            cancellationToken);

        await SendConversationUpdatedEvents(
            activeParticipants,
            messageResponse,
            messageResponse.SentAt,
            cancellationToken);

        return offer.NegotiationOfferId;
    }

    private static void ValidateRequest(
        decimal finalPrice,
        DateOnly? startDate,
        DateOnly? endDate,
        IReadOnlyCollection<Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs.NegotiationMilestoneDto>? milestones)
    {
        if (!ProposalTotalsCalculator.IsValidAmount(finalPrice))
        {
            throw new BadRequestException("Final price must be greater than zero, use at most 2 decimal places, and fit decimal(18,2).");
        }

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new BadRequestException("Start date must be before or equal to end date.");
        }

        if (milestones is null || milestones.Count == 0)
        {
            throw new BadRequestException("At least one milestone is required for a final offer.");
        }

        if (milestones.Select(item => item.OrderIndex).Distinct().Count() != milestones.Count)
        {
            throw new BadRequestException("Milestone order indexes must be unique.");
        }

        if (milestones.Any(item => string.IsNullOrWhiteSpace(item.Title) ||
                                   string.IsNullOrWhiteSpace(item.Deliverables) ||
                                   string.IsNullOrWhiteSpace(item.AcceptanceCriteria) ||
                                   !ProposalTotalsCalculator.IsValidAmount(item.Amount)))
        {
            throw new BadRequestException("Each milestone requires a title, positive amount, deliverables, and acceptance criteria.");
        }

        if (milestones.Any(item => item.WorkItems.Count == 0 ||
                                   item.WorkItems.Any(workItem =>
                                       string.IsNullOrWhiteSpace(workItem.Title) ||
                                       string.IsNullOrWhiteSpace(workItem.Description)) ||
                                   item.WorkItems.Select(workItem => workItem.OrderIndex).Distinct().Count() != item.WorkItems.Count))
        {
            throw new BadRequestException("Each milestone requires work items with title, description, and unique order indexes.");
        }

        if (milestones.Any(item => !MilestoneDeadlineCalculator.TryParseDurationDays(item.EstimatedDuration, out _)))
        {
            throw new BadRequestException("Each milestone duration must be a positive whole number in week(s), month(s), or year(s).");
        }

        if (milestones.Sum(item => item.Amount) != finalPrice)
        {
            throw new BadRequestException("Milestone total must equal the final price.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Message AddConversationMessage(
        Conversation conversation,
        Guid senderUserId,
        MessageType messageType,
        string content,
        string? metadata,
        DateTime now)
    {
        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            SenderUserId = senderUserId,
            MessageType = (int)messageType,
            Content = content,
            Metadata = metadata,
            SentAt = now
        };

        _context.Set<Message>().Add(message);
        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;

        return message;
    }

    private Task<List<ConversationParticipant>> GetActiveParticipants(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(participant =>
                participant.ConversationsId == conversationId &&
                participant.LeftAt == null &&
                participant.DeletedAt == null)
            .ToListAsync(cancellationToken);
    }

    private async Task SendConversationUpdatedEvents(
        IReadOnlyCollection<ConversationParticipant> participants,
        MessageResponse lastMessage,
        DateTime lastMessageAt,
        CancellationToken cancellationToken)
    {
        foreach (var participant in participants
            .GroupBy(participant => participant.UserId)
            .Select(group => group.First()))
        {
            await _chatRealtimeNotifier.SendUserEventAsync(
                participant.UserId,
                "ConversationUpdated",
                new
                {
                    conversationId = lastMessage.ConversationId,
                    lastMessage,
                    lastMessageAt,
                    unreadCount = participant.UnreadCount
                },
                cancellationToken);
        }
    }

    private static MessageResponse ToMessageResponse(Message message)
    {
        var isDeleted = message.DeletedForEveryoneAt.HasValue;

        return new MessageResponse(
            message.MessagesId,
            message.ConversationsId,
            message.SenderUserId,
            message.MessageType,
            isDeleted ? null : message.Content,
            message.ReplyToMessageId,
            isDeleted ? null : message.Metadata,
            message.ClientMessageId,
            message.SentAt,
            isDeleted ? null : message.EditedAt,
            isDeleted,
            []);
    }

    private void IncrementUnreadCounts(Guid conversationId, Guid senderUserId)
    {
        var participants = _context.Set<ConversationParticipant>()
            .Where(participant =>
                participant.ConversationsId == conversationId &&
                participant.LeftAt == null);

        foreach (var participant in participants)
        {
            if (participant.UserId != senderUserId)
            {
                participant.UnreadCount += 1;
            }
        }
    }
}
