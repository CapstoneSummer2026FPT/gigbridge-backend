using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
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
        ValidateRequest(request.FinalPrice, request.StartDate, request.EndDate);

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

        var message = AddConversationMessage(
            conversation,
            command.UserId,
            MessageType.FinalOffer,
            "Final offer sent.",
            JsonSerializer.Serialize(new { negotiationOfferId = offer.NegotiationOfferId }),
            now);

        IncrementUnreadCounts(conversation.ConversationsId, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        await _chatRealtimeNotifier.SendConversationEventAsync(
            conversation.ConversationsId,
            "FinalOfferCreated",
            new { offerId = offer.NegotiationOfferId, messageId = message.MessagesId },
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
        DateOnly? endDate)
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
    }

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
