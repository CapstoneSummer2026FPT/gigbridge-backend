using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
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

        ValidateRequest(request.FinalPrice, request.StartDate, request.EndDate, request.Milestones);

        var contract = await GetContract(conversation.ContractsId, cancellationToken);

        if (contract.Status != (int)ContractStatus.Draft &&
            contract.Status != (int)ContractStatus.PendingFreelancerSelection &&
            contract.Status != (int)ContractStatus.InNegotiation)
        {
            throw new BadRequestException("Final offers can only be created while the contract is being negotiated.");
        }

        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null || clientProfile.ClientProfilesId != contract.ClientProfilesId)
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
            ContractsId = contract.ContractsId,
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

        foreach (var milestone in request.Milestones!.OrderBy(item => item.OrderIndex))
        {
            var draftId = Guid.NewGuid();
            _context.Set<NegotiationMilestoneDraft>().Add(new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = draftId,
                ConversationsId = request.ConversationId,
                Title = milestone.Title!.Trim(),
                Description = Clean(milestone.Description),
                Amount = milestone.Amount,
                EstimatedDuration = Clean(milestone.EstimatedDuration),
                DueDate = milestone.DueDate,
                Deliverables = milestone.Deliverables!.Trim(),
                AcceptanceCriteria = milestone.AcceptanceCriteria!.Trim(),
                OrderIndex = milestone.OrderIndex,
                CreatedAt = now,
                UpdatedAt = now
            });
            _context.Set<NegotiationOfferMilestone>().Add(new NegotiationOfferMilestone
            {
                NegotiationOfferMilestoneId = Guid.NewGuid(),
                NegotiationOfferId = offer.NegotiationOfferId,
                Title = milestone.Title.Trim(),
                Description = Clean(milestone.Description),
                Amount = milestone.Amount,
                EstimatedDuration = Clean(milestone.EstimatedDuration),
                DueDate = milestone.DueDate,
                Deliverables = milestone.Deliverables.Trim(),
                AcceptanceCriteria = milestone.AcceptanceCriteria.Trim(),
                OrderIndex = milestone.OrderIndex
            });
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
            "FinalOfferCreated",
            new { offerId = offer.NegotiationOfferId, messageId = message.MessagesId },
            cancellationToken);

        await SendConversationUpdatedEvents(
            activeParticipants,
            messageResponse,
            messageResponse.SentAt,
            cancellationToken);

        return offer.NegotiationOfferId;
    }

    private async Task<Contract> GetContract(
        Guid? contractId,
        CancellationToken cancellationToken)
    {
        if (!contractId.HasValue)
        {
            throw new BadRequestException("Negotiation conversation is not attached to a contract draft.");
        }

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == contractId.Value, cancellationToken);

        return contract ?? throw new NotFoundException("Contract draft does not exist.");
    }

    private static void ValidateRequest(
        decimal finalPrice,
        DateOnly? startDate,
        DateOnly? endDate,
        IReadOnlyCollection<Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs.NegotiationMilestoneDto>? milestones)
    {
        if (finalPrice <= 0)
        {
            throw new BadRequestException("Final price must be greater than zero.");
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
                                   item.Amount <= 0))
        {
            throw new BadRequestException("Each milestone requires a title, positive amount, deliverables, and acceptance criteria.");
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
