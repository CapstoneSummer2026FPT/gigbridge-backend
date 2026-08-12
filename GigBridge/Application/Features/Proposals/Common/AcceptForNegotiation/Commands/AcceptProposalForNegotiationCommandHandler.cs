using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common.Email;
using Application.Features.Proposals.Common;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Notifications;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.Proposals.Common.AcceptForNegotiation.Commands;

public class AcceptProposalForNegotiationCommandHandler : IRequestHandler<AcceptProposalForNegotiationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IProposalNegotiationEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AcceptProposalForNegotiationCommandHandler> _logger;

    public AcceptProposalForNegotiationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notificationService,
        IEmailService emailService,
        IProposalNegotiationEmailRenderer emailRenderer,
        IConfiguration configuration,
        ILogger<AcceptProposalForNegotiationCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid> Handle(AcceptProposalForNegotiationCommand command, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .Include(cp => cp.User)
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new ForbiddenAccessException("Only clients can accept a proposal for negotiation.");
        }

        var proposal = await _context.Set<Proposal>()
            .Include(p => p.JobPosts)
            .Include(p => p.FreelancerProfiles)
                .ThenInclude(fp => fp.User)
            .Include(p => p.ProposalMilestonePlans)
            .Include(p => p.ProposalWorkBreakdownItems)
            .FirstOrDefaultAsync(p => p.ProposalsId == command.ProposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        ProposalModerationGuard.EnsureActive(proposal);

        if (proposal.JobPosts.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not own this job post.");
        }

        JobPostNegotiationGuard.EnsureEligibleForNegotiation(proposal.JobPosts);

        if (proposal.Status != 1 && proposal.Status != 2 && proposal.Status != 3) // Accepted may reopen its existing negotiation.
        {
            throw new BadRequestException("Proposal must be Pending, Shortlisted, or Accepted to open negotiation.");
        }

        var shouldNotifyNegotiationStart = proposal.Status is 1 or 2;

        var now = _dateTimeService.UtcNow;

        await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
            _context,
            proposal.JobPostsId,
            proposal.FreelancerProfilesId,
            TalentMatchEventType.Shortlisted,
            proposal.ProposalsId,
            now,
            cancellationToken);
        var existingConversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                c => c.ConversationType == (int)ConversationType.JobNegotiation &&
                     c.JobPostsId == proposal.JobPostsId &&
                     c.ProposalsId == proposal.ProposalsId &&
                     c.DeletedAt == null,
                cancellationToken);

        bool isFirstTime = existingConversation == null;
        Guid conversationId;

        if (existingConversation is not null)
        {
            conversationId = existingConversation.ConversationsId;
            await EnsureParticipants(conversationId, clientProfile.UserId, proposal.FreelancerProfiles.UserId, cancellationToken);

            await ProposalMilestoneHandoff.SeedConversationDraftAsync(
                _context, conversationId, proposal, now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await NotifyConversationUpdated(conversationId, existingConversation.LastMessageAt, cancellationToken);
        }
        else
        {
            var conversation = new Conversation
            {
                ConversationsId = Guid.NewGuid(),
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = proposal.JobPostsId,
                ProposalsId = proposal.ProposalsId,
                ContractsId = null,
                CreatedByUserId = command.UserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = now
            };

            _context.Set<Conversation>().Add(conversation);
            conversationId = conversation.ConversationsId;

            AddParticipant(conversationId, clientProfile.UserId, ParticipantRole.Client, now);
            AddParticipant(conversationId, proposal.FreelancerProfiles.UserId, ParticipantRole.Freelancer, now);

            await ProposalMilestoneHandoff.SeedConversationDraftAsync(
                _context, conversationId, proposal, now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await NotifyConversationUpdated(conversationId, conversation.LastMessageAt, cancellationToken);
        }

        if (isFirstTime && shouldNotifyNegotiationStart)
        {
            if (proposal.Status == 1) // Pending
            {
                proposal.Status = 2; // Shortlisted
                proposal.UpdatedAt = now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Create notification
            await _notificationService.CreateNotificationAsync(
                proposal.FreelancerProfiles.UserId,
                NotificationType.ProposalStatusChanged,
                "Proposal accepted for negotiation",
                $"Your proposal for \"{proposal.JobPosts.Title}\" was accepted for negotiation.",
                proposal.ProposalsId,
                "Proposal",
                cancellationToken);

            // Send email
            var freelancerUser = proposal.FreelancerProfiles.User;
            if (freelancerUser != null)
            {
                try
                {
                    var frontendUrl = _configuration["FrontendBaseUrl"] ?? "http://localhost:5173";
                    var actionUrl = $"{frontendUrl.TrimEnd('/')}/messages?conversationId={conversationId}";
                    var clientName = clientProfile.CompanyName ?? clientProfile.User.FullName;

                    var emailModel = new ProposalNegotiationEmailModel(
                        FreelancerName: freelancerUser.FullName,
                        ClientName: clientName,
                        JobTitle: proposal.JobPosts.Title,
                        ProposedBudget: proposal.ProposedBudget.HasValue ? $"${proposal.ProposedBudget.Value:F0}" : "Not specified",
                        ProposedDuration: proposal.ProposedDuration ?? "Not specified",
                        ActionUrl: actionUrl
                    );

                    var emailCopy = _emailRenderer.Render(emailModel);

                    await _emailService.SendEmailAsync(new EmailRequest
                    {
                        To = freelancerUser.Email,
                        Subject = emailCopy.Subject,
                        Body = emailCopy.HtmlBody,
                        TextBody = emailCopy.TextBody,
                        IsHtml = true
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send proposal negotiation email to freelancer user {UserId}",
                        freelancerUser.UserId);
                }
            }
        }

        return conversationId;
    }

    private async Task EnsureParticipants(Guid conversationId, Guid clientUserId, Guid freelancerUserId, CancellationToken cancellationToken)
    {
        var participants = await _context.Set<ConversationParticipant>()
            .Where(p => p.ConversationsId == conversationId)
            .ToListAsync(cancellationToken);
        var now = _dateTimeService.UtcNow;

        if (!participants.Any(p => p.UserId == clientUserId))
        {
            AddParticipant(conversationId, clientUserId, ParticipantRole.Client, now);
        }

        if (!participants.Any(p => p.UserId == freelancerUserId))
        {
            AddParticipant(conversationId, freelancerUserId, ParticipantRole.Freelancer, now);
        }
    }

    private void AddParticipant(Guid conversationId, Guid userId, ParticipantRole role, DateTime now)
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

    private async Task NotifyConversationUpdated(Guid conversationId, DateTime? lastMessageAt, CancellationToken cancellationToken)
    {
        var participants = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(p => p.ConversationsId == conversationId && p.LeftAt == null && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants.GroupBy(p => p.UserId).Select(g => g.First()))
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
