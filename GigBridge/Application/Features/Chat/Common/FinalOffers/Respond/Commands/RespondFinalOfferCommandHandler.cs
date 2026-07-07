using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.FinalOffers.Respond.Commands;

public class RespondFinalOfferCommandHandler : IRequestHandler<RespondFinalOfferCommand, RespondFinalOfferResponse>
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

    public async Task<RespondFinalOfferResponse> Handle(
        RespondFinalOfferCommand command,
        CancellationToken cancellationToken)
    {
        var offer = await _context.Set<NegotiationOffer>()
            .Include(item => item.NegotiationOfferMilestones)
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
        string eventName;
        RespondFinalOfferResponse response;

        switch (command.Request.Response)
        {
            case FinalOfferResponse.Accept:
                response = await AcceptOffer(offer, conversation, command.UserId, now, cancellationToken);
                eventName = "ContractDraftUpdated";
                break;
            case FinalOfferResponse.RequestChange:
                eventName = ChangeOfferStatus(
                    offer,
                    conversation,
                    NegotiationOfferStatus.ChangeRequested,
                    "Final offer change requested.",
                    now);
                response = new RespondFinalOfferResponse(null, null, "Final offer change requested.");
                break;
            case FinalOfferResponse.Decline:
                eventName = ChangeOfferStatus(
                    offer,
                    conversation,
                    NegotiationOfferStatus.Rejected,
                    "Final offer declined.",
                    now);
                response = new RespondFinalOfferResponse(null, null, "Final offer declined.");
                break;
            default:
                throw new BadRequestException("Unsupported final offer response.");
        }

        IncrementUnreadCounts(conversation.ConversationsId, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var activeParticipants = await GetActiveParticipants(
            conversation.ConversationsId,
            cancellationToken);
        var participantUserIds = activeParticipants
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArray();

        await _chatRealtimeNotifier.SendUsersEventAsync(
            participantUserIds,
            "FinalOfferResponded",
            new
            {
                offerId = offer.NegotiationOfferId,
                status = offer.Status,
                response = command.Request.Response.ToString()
            },
            cancellationToken);

        if (conversation.LastMessageId.HasValue)
        {
            var lastMessage = await _context.Set<Message>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    message => message.MessagesId == conversation.LastMessageId.Value,
                    cancellationToken);

            if (lastMessage is not null)
            {
                var messageResponse = ToMessageResponse(lastMessage);

                await SendConversationUpdatedEvents(
                    activeParticipants,
                    messageResponse,
                    messageResponse.SentAt,
                    cancellationToken);
            }
        }

        if (eventName == "ContractDraftUpdated")
        {
            await _chatRealtimeNotifier.SendUsersEventAsync(
                participantUserIds,
                eventName,
                new { contractId = offer.ContractsId },
                cancellationToken);
        }

        return response;
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

    private async Task<RespondFinalOfferResponse> AcceptOffer(
        NegotiationOffer offer,
        Conversation conversation,
        Guid userId,
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

        if (offer.NegotiationOfferMilestones.Count == 0)
        {
            throw new BadRequestException("The final offer does not contain a milestone snapshot.");
        }

        if (offer.NegotiationOfferMilestones.Sum(item => item.Amount) != offer.FinalPrice)
        {
            throw new BadRequestException("The final offer milestone total does not match its final price.");
        }

        var existingMilestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);
        _context.Set<Milestone>().RemoveRange(existingMilestones);

        foreach (var snapshot in offer.NegotiationOfferMilestones.OrderBy(item => item.OrderIndex))
        {
            _context.Set<Milestone>().Add(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                Title = snapshot.Title,
                Description = snapshot.Description,
                Amount = snapshot.Amount,
                EstimatedDuration = snapshot.EstimatedDuration,
                DueDate = snapshot.DueDate,
                Deliverables = snapshot.Deliverables,
                AcceptanceCriteria = snapshot.AcceptanceCriteria,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = snapshot.OrderIndex,
                ReleasedAmount = 0m,
                CreatedAt = now
            });
        }

        await ServiceFeeWorkflow.ChargeAsync(
            _context,
            userId,
            contract.ContractsId,
            offer.FinalPrice,
            $"{ServiceFeeWorkflow.AcceptJobFeePrefix}{offer.NegotiationOfferId:N}",
            $"1% service fee for accepting the job: {contract.Title}.",
            now,
            cancellationToken);

        offer.Status = (int)NegotiationOfferStatus.Accepted;
        offer.RespondedAt = now;

        contract.FreelancerProfilesId = offer.FreelancerProfilesId;
        contract.ProposalsId = offer.ProposalsId;
        contract.TotalBudget = offer.FinalPrice;
        contract.StartDate = offer.StartDate;
        contract.EndDate = offer.EndDate;
        contract.Status = (int)ContractStatus.PendingSignature;
        contract.UpdatedAt = now;

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(existing => existing.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                RequiredAmount = offer.FinalPrice,
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
            escrow.RequiredAmount = offer.FinalPrice;
            escrow.FundedAmount = 0m;
            escrow.RequiredPercentage = 1.0m;
            escrow.Currency = string.IsNullOrWhiteSpace(escrow.Currency) ? "VND" : escrow.Currency;
            escrow.Status = (int)ContractEscrowStatus.PendingFunding;
            escrow.FundedAt = null;
            escrow.ReleasedAt = null;
        }

        if (offer.ProposalsId.HasValue)
        {
            var proposal = await _context.Set<Proposal>()
                .FirstOrDefaultAsync(
                    proposal => proposal.ProposalsId == offer.ProposalsId.Value,
                    cancellationToken);

            if (proposal is not null)
            {
                proposal.Status = 3;
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

        // Decouple other conversations from the same JobPost by setting ContractsId to null
        var otherConversations = await _context.Set<Conversation>()
            .Where(c => c.JobPostsId == offer.JobPostsId && c.ConversationsId != conversation.ConversationsId)
            .ToListAsync(cancellationToken);

        foreach (var otherConv in otherConversations)
        {
            otherConv.ContractsId = null;
            otherConv.UpdatedAt = now;
        }

        AddSystemMessage(conversation, "Final offer accepted. Contract is ready for signatures.", now);

        return new RespondFinalOfferResponse(
            contract.ContractsId,
            contract.Status,
            "Final offer accepted. Contract is ready for signatures.");
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
