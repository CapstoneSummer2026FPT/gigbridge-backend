using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Negotiations.OpenFromInvite.Commands;

public class OpenNegotiationFromInviteCommandHandler
    : IRequestHandler<OpenNegotiationFromInviteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public OpenNegotiationFromInviteCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
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

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == command.FreelancerProfileId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(
                contract => contract.JobPostsId == command.JobPostId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract draft does not exist for this job post.");
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
            return existingConversation.ConversationsId;
        }

        var now = _dateTimeService.UtcNow;
        var conversation = new Conversation
        {
            ConversationsId = Guid.NewGuid(),
            ConversationType = (int)ConversationType.JobNegotiation,
            JobPostsId = command.JobPostId,
            ContractsId = contract.ContractsId,
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

        contract.Status = (int)ContractStatus.InNegotiation;
        contract.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return conversation.ConversationsId;
    }
}
