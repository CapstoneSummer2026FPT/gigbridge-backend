using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Application.Features.JobInvitations.Common.Email;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobInvitations.Client.CreateInvitation.Commands;

public sealed class CreateJobInvitationCommandHandler
    : IRequestHandler<CreateJobInvitationCommand, JobInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IJobInvitationEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateJobInvitationCommandHandler> _logger;

    public CreateJobInvitationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IEmailService emailService,
        IJobInvitationEmailRenderer emailRenderer,
        IConfiguration configuration,
        ILogger<CreateJobInvitationCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<JobInvitationDto> Handle(
        CreateJobInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var jobPost = await JobInvitationRules.GetOwnedOpenJobPostAsync(
            _context,
            command.Request.JobPostId,
            clientProfile.ClientProfilesId,
            cancellationToken);

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == command.Request.FreelancerProfileId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var alreadyInvited = await _context.Set<JobInvitation>()
            .AnyAsync(invitation =>
                invitation.JobPostsId == jobPost.JobPostsId &&
                invitation.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (alreadyInvited)
        {
            throw new ConflictException("This freelancer was already invited to this job post.");
        }

        var invitation = new JobInvitation
        {
            JobInvitationsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            ClientProfilesId = clientProfile.ClientProfilesId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            Status = (int)JobInvitationStatus.Pending,
            Message = JobInvitationRules.CleanMessage(command.Request.Message),
            ExpiresAt = command.Request.ExpiresAt,
            CreatedAt = _dateTimeService.UtcNow
        };

        _context.Set<JobInvitation>().Add(invitation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException("This freelancer was already invited to this job post.", exception);
        }

        await NotifyFreelancerAsync(freelancerProfile.UserId, invitation.JobInvitationsId, jobPost.Title, cancellationToken);
        await SendInvitationEmailAsync(
            freelancerProfile.User,
            clientProfile,
            jobPost,
            cancellationToken);

        return await _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(item => item.JobInvitationsId == invitation.JobInvitationsId)
            .ProjectToJobInvitationDto()
            .FirstAsync(cancellationToken);
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
        User freelancerUser,
        ClientProfile clientProfile,
        JobPost jobPost,
        CancellationToken cancellationToken)
    {
        try
        {
            var clientUserName = await _context.Set<User>()
                .AsNoTracking()
                .Where(user => user.UserId == clientProfile.UserId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            var clientName = clientProfile.CompanyName ?? clientUserName ?? "A client";
            var frontendUrl = _configuration["FrontendBaseUrl"] ?? "http://localhost:5173";
            var actionUrl = $"{frontendUrl.TrimEnd('/')}/jobs/{jobPost.JobPostsId}";
            var emailModel = new NewJobInvitationTemplate(
                FreelancerName: freelancerUser.FullName,
                JobTitle: jobPost.Title,
                ClientName: clientName,
                Budget: FormatBudget(jobPost),
                Deadline: FormatDeadline(jobPost),
                ShortDescription: BuildShortDescription(jobPost.Description),
                ActionUrl: actionUrl);

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
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send job invitation email to freelancer {Email}.", freelancerUser.Email);
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
}
