using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Application.Common.InternalServices.JobInvitations.Email;
using Application.Common.InternalServices.JobInvitations.Interfaces;
using Application.Common.InternalServices.JobInvitations.Models;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums.JobInvitations;
using Domain.Enums.Notifications;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Common.Models.Email;

namespace Application.Features.JobInvitations.Client.BulkCreateInvitations.Commands;

public sealed class BulkCreateJobInvitationsCommandHandler
    : IRequestHandler<BulkCreateJobInvitationsCommand, BulkJobInvitationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IJobInvitationEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BulkCreateJobInvitationsCommandHandler> _logger;

    public BulkCreateJobInvitationsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IEmailService emailService,
        IJobInvitationEmailRenderer emailRenderer,
        IConfiguration configuration,
        ILogger<BulkCreateJobInvitationsCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BulkJobInvitationResultDto> Handle(
        BulkCreateJobInvitationsCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var jobPostIds = command.Request.JobPostIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var freelancerProfileIds = command.Request.FreelancerProfileIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var jobsById = await _context.Set<JobPost>()
            .Where(jobPost => jobPostIds.Contains(jobPost.JobPostsId))
            .ToDictionaryAsync(jobPost => jobPost.JobPostsId, cancellationToken);

        var freelancersById = await _context.Set<FreelancerProfile>()
            .Include(profile => profile.User)
            .Where(profile => freelancerProfileIds.Contains(profile.FreelancerProfilesId))
            .ToDictionaryAsync(profile => profile.FreelancerProfilesId, cancellationToken);

        var existingPairs = (await _context.Set<JobInvitation>()
                .AsNoTracking()
                .Where(invitation =>
                    jobPostIds.Contains(invitation.JobPostsId) &&
                    freelancerProfileIds.Contains(invitation.FreelancerProfilesId))
                .Select(invitation => new
                {
                    invitation.JobPostsId,
                    invitation.FreelancerProfilesId
                })
                .ToListAsync(cancellationToken))
            .Select(pair => (pair.JobPostsId, pair.FreelancerProfilesId))
            .ToHashSet();

        var result = new BulkJobInvitationResultDto();
        var createdIds = new List<Guid>();
        var notificationPayloads = new List<(Guid FreelancerUserId, Guid InvitationId, string JobTitle)>();
        var emailPayloads = new List<JobInvitationEmailPayload>();
        var now = _dateTimeService.UtcNow;
        var message = JobInvitationRules.CleanMessage(command.Request.Message);
        var clientUserName = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == clientProfile.UserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        var clientName = clientProfile.CompanyName ?? clientUserName ?? "A client";

        foreach (var jobPostId in jobPostIds)
        {
            jobsById.TryGetValue(jobPostId, out var jobPost);

            foreach (var freelancerProfileId in freelancerProfileIds)
            {
                if (jobPost is null)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Job post does not exist.");
                    continue;
                }

                if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "You do not own this job post.");
                    continue;
                }

                if (jobPost.Status != JobInvitationRules.OpenJobPostStatus)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Job post is not open.");
                    continue;
                }

                if (!freelancersById.TryGetValue(freelancerProfileId, out var freelancerProfile))
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Freelancer profile does not exist.");
                    continue;
                }

                if (existingPairs.Contains((jobPostId, freelancerProfileId)))
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "This freelancer was already invited to this job post.");
                    continue;
                }

                var invitation = new JobInvitation
                {
                    JobInvitationsId = Guid.NewGuid(),
                    JobPostsId = jobPostId,
                    ClientProfilesId = clientProfile.ClientProfilesId,
                    FreelancerProfilesId = freelancerProfileId,
                    Status = (int)JobInvitationStatus.Pending,
                    Message = message,
                    ExpiresAt = command.Request.ExpiresAt,
                    CreatedAt = now
                };

                _context.Set<JobInvitation>().Add(invitation);
                if (command.Request.MatchRunId.HasValue)
                {
                    await TalentMatchFeedbackWriter.TryAddForRunAsync(
                        _context,
                        command.Request.MatchRunId.Value,
                        command.UserId,
                        jobPostId,
                        freelancerProfileId,
                        TalentMatchEventType.Invited,
                        $"match:{command.Request.MatchRunId.Value:N}:invited:{invitation.JobInvitationsId:N}",
                        invitation.JobInvitationsId,
                        now,
                        cancellationToken);
                }
                createdIds.Add(invitation.JobInvitationsId);
                existingPairs.Add((jobPostId, freelancerProfileId));
                notificationPayloads.Add((freelancerProfile.UserId, invitation.JobInvitationsId, jobPost.Title));
                emailPayloads.Add(new JobInvitationEmailPayload(freelancerProfile.User, clientName, jobPost));
            }
        }

        if (createdIds.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            result.Created = await _context.Set<JobInvitation>()
                .AsNoTracking()
                .Where(invitation => createdIds.Contains(invitation.JobInvitationsId))
                .OrderByDescending(invitation => invitation.CreatedAt)
                .ProjectToJobInvitationDto()
                .ToListAsync(cancellationToken);

            foreach (var payload in notificationPayloads)
            {
                await NotifyFreelancerAsync(payload.FreelancerUserId, payload.InvitationId, payload.JobTitle, cancellationToken);
            }

            foreach (var payload in emailPayloads)
            {
                await SendInvitationEmailAsync(payload, cancellationToken);
            }
        }

        return result;
    }

    private static void AddSkipped(
        BulkJobInvitationResultDto result,
        Guid jobPostId,
        Guid freelancerProfileId,
        string reason)
    {
        result.Skipped.Add(new BulkJobInvitationSkipDto
        {
            JobPostId = jobPostId,
            FreelancerProfileId = freelancerProfileId,
            Reason = reason
        });
    }

    private async Task NotifyFreelancerAsync(
        Guid freelancerUserId,
        Guid invitationId,
        string jobTitle,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.CreateNotificationAsync(
                freelancerUserId,
                NotificationType.SystemAlert,
                "New job invitation",
                $"You were invited to apply for \"{jobTitle}\".",
                invitationId,
                "JobInvitation",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send job invitation notification {InvitationId}.", invitationId);
        }
    }

    private async Task SendInvitationEmailAsync(
        JobInvitationEmailPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var frontendUrl = _configuration["FrontendBaseUrl"] ?? "http://localhost:5173";
            var actionUrl = $"{frontendUrl.TrimEnd('/')}/jobs/{payload.JobPost.JobPostsId}";
            var emailModel = new NewJobInvitationTemplate(
                FreelancerName: payload.FreelancerUser.FullName,
                JobTitle: payload.JobPost.Title,
                ClientName: payload.ClientName,
                Budget: FormatBudget(payload.JobPost),
                Deadline: FormatDeadline(payload.JobPost),
                ShortDescription: BuildShortDescription(payload.JobPost.Description),
                ActionUrl: actionUrl);

            var emailCopy = _emailRenderer.Render(emailModel);

            await _emailService.SendEmailAsync(new EmailRequest
            {
                To = payload.FreelancerUser.Email,
                Subject = emailCopy.Subject,
                Body = emailCopy.HtmlBody,
                TextBody = emailCopy.TextBody,
                IsHtml = true
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to send job invitation email to freelancer user {UserId}.",
                payload.FreelancerUser.UserId);
        }
    }

    private static string FormatBudget(JobPost jobPost)
    {
        var currency = string.IsNullOrWhiteSpace(jobPost.Currency) ? "VND" : jobPost.Currency;

        return (jobPost.BudgetMin, jobPost.BudgetMax) switch
        {
            ({ } min, { } max) when min != max => $"{min:N0} - {max:N0} {currency}",
            ({ } min, _) => $"{min:N0} {currency}",
            (_, { } max) => $"{max:N0} {currency}",
            _ => "Not specified"
        };
    }

    private static string FormatDeadline(JobPost jobPost)
    {
        if (jobPost.EndDate.HasValue)
        {
            return jobPost.EndDate.Value.ToString("yyyy-MM-dd");
        }

        return string.IsNullOrWhiteSpace(jobPost.EstimatedDuration)
            ? "Not specified"
            : jobPost.EstimatedDuration;
    }

    private static string BuildShortDescription(string description)
    {
        const int maxLength = 240;
        var normalized = string.Join(" ", description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..maxLength]}...";
    }

    private sealed record JobInvitationEmailPayload(User FreelancerUser, string ClientName, JobPost JobPost);
}
