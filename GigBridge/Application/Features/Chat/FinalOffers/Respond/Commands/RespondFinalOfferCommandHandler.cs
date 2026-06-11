using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.FinalOffers.Respond.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.FinalOffers.Respond.Commands;

public class RespondFinalOfferCommandHandler : IRequestHandler<RespondFinalOfferCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public RespondFinalOfferCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<bool> Handle(
        RespondFinalOfferCommand command,
        CancellationToken cancellationToken)
    {
        var offer = await _context.Set<NegotiationOffer>()
            .FirstOrDefaultAsync(
                offer => offer.NegotiationOfferId == command.Request.NegotiationOfferId,
                cancellationToken);

        if (offer is null)
        {
            throw new NotFoundException("Negotiation offer does not exist.");
        }

        if (offer.Status != (int)NegotiationOfferStatus.PendingFreelancerConfirmation)
        {
            throw new BadRequestException("Only pending final offers can be responded to.");
        }

        var participant = await _context.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == offer.ConversationsId &&
                    participant.UserId == command.UserId &&
                    participant.ParticipantRole == (int)ParticipantRole.Freelancer &&
                    participant.LeftAt == null,
                cancellationToken);

        if (participant is null)
        {
            throw new ForbiddenAccessException("Only the freelancer participant can respond to this final offer.");
        }

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (freelancerProfile is null ||
            freelancerProfile.FreelancerProfilesId != offer.FreelancerProfilesId)
        {
            throw new ForbiddenAccessException("You are not the freelancer selected for this final offer.");
        }

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                conversation => conversation.ConversationsId == offer.ConversationsId,
                cancellationToken);

        if (conversation is null)
        {
            throw new NotFoundException("Conversation does not exist.");
        }

        var now = _dateTimeService.UtcNow;
        var eventName = command.Request.Response switch
        {
            FinalOfferResponse.Accept => await AcceptOffer(offer, conversation, now, cancellationToken),
            FinalOfferResponse.RequestChange => ChangeOfferStatus(
                offer,
                conversation,
                NegotiationOfferStatus.ChangeRequested,
                "Final offer change requested.",
                now),
            FinalOfferResponse.Decline => ChangeOfferStatus(
                offer,
                conversation,
                NegotiationOfferStatus.Rejected,
                "Final offer declined.",
                now),
            _ => throw new BadRequestException("Unsupported final offer response.")
        };

        IncrementUnreadCounts(conversation.ConversationsId, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        await _chatRealtimeNotifier.SendConversationEventAsync(
            conversation.ConversationsId,
            "FinalOfferResponded",
            new
            {
                offerId = offer.NegotiationOfferId,
                status = offer.Status,
                response = command.Request.Response.ToString()
            },
            cancellationToken);

        if (eventName == "ContractDraftUpdated")
        {
            await _chatRealtimeNotifier.SendConversationEventAsync(
                conversation.ConversationsId,
                eventName,
                new { contractId = offer.ContractsId },
                cancellationToken);
        }

        return true;
    }

    private async Task<string> AcceptOffer(
        NegotiationOffer offer,
        Conversation conversation,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyAccepted = await _context.Set<NegotiationOffer>()
            .AnyAsync(
                existingOffer =>
                    existingOffer.JobPostsId == offer.JobPostsId &&
                    existingOffer.NegotiationOfferId != offer.NegotiationOfferId &&
                    existingOffer.Status == (int)NegotiationOfferStatus.Accepted,
                cancellationToken);

        if (alreadyAccepted)
        {
            throw new ConflictException("A final offer has already been accepted for this job post.");
        }

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(
                contract => contract.ContractsId == offer.ContractsId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract draft does not exist.");
        }

        if (contract.Status != (int)ContractStatus.Draft &&
            contract.Status != (int)ContractStatus.PendingFreelancerSelection &&
            contract.Status != (int)ContractStatus.InNegotiation)
        {
            throw new BadRequestException("The contract draft can no longer accept a final offer.");
        }

        offer.Status = (int)NegotiationOfferStatus.Accepted;
        offer.RespondedAt = now;

        contract.FreelancerProfilesId = offer.FreelancerProfilesId;
        contract.ProposalsId = offer.ProposalsId;
        contract.TotalBudget = offer.FinalPrice;
        contract.StartDate = offer.StartDate;
        contract.EndDate = offer.EndDate;
        contract.Status = (int)ContractStatus.PendingContractDetails;
        contract.UpdatedAt = now;

        if (offer.ProposalsId.HasValue)
        {
            var proposal = await _context.Set<Proposal>()
                .FirstOrDefaultAsync(
                    proposal => proposal.ProposalsId == offer.ProposalsId.Value,
                    cancellationToken);

            if (proposal is not null)
            {
                proposal.Status = 2;
                proposal.UpdatedAt = now;
            }
        }

        var otherPendingOffers = await _context.Set<NegotiationOffer>()
            .Where(otherOffer =>
                otherOffer.JobPostsId == offer.JobPostsId &&
                otherOffer.NegotiationOfferId != offer.NegotiationOfferId &&
                otherOffer.Status == (int)NegotiationOfferStatus.PendingFreelancerConfirmation)
            .ToListAsync(cancellationToken);

        foreach (var pendingOffer in otherPendingOffers)
        {
            pendingOffer.Status = (int)NegotiationOfferStatus.Cancelled;
            pendingOffer.RespondedAt = now;
        }

        AddSystemMessage(conversation, "Final offer accepted. Contract draft is ready for details.", now);

        return "ContractDraftUpdated";
    }

    private string ChangeOfferStatus(
        NegotiationOffer offer,
        Conversation conversation,
        NegotiationOfferStatus status,
        string message,
        DateTime now)
    {
        offer.Status = (int)status;
        offer.RespondedAt = now;
        AddSystemMessage(conversation, message, now);

        return "FinalOfferResponded";
    }

    private void AddSystemMessage(
        Conversation conversation,
        string content,
        DateTime now)
    {
        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            SenderUserId = null,
            MessageType = (int)MessageType.ContractEvent,
            Content = content,
            SentAt = now
        };

        _context.Set<Message>().Add(message);
        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
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
