using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Common.Services;

public sealed class DeliveryOutboxService : BackgroundService
{
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(6)];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryOutboxService> _logger;
    private readonly IScheduleEmailRenderer _emailRenderer;
    private readonly string _frontendBaseUrl;

    public DeliveryOutboxService(IServiceScopeFactory scopeFactory, ILogger<DeliveryOutboxService> logger,
        IConfiguration configuration, IScheduleEmailRenderer emailRenderer)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _emailRenderer = emailRenderer;
        _frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatch(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Schedule delivery outbox batch failed."); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    internal async Task ProcessBatch(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = DateTime.UtcNow;
        await EnsureAcceptedSchedulesHaveStartDeliveries(context, now, _frontendBaseUrl, _emailRenderer, ct);
        await context.Set<DeliveryOutbox>()
            .Where(x => x.Status == (int)DeliveryOutboxStatus.Processing && x.NextAttemptAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, (int)DeliveryOutboxStatus.Pending)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.NextAttemptAt, now), ct);
        var candidates = await context.Set<DeliveryOutbox>().AsNoTracking().Where(x => x.Status == (int)DeliveryOutboxStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Select(x => x.DeliveryOutboxId).Take(25).ToListAsync(ct);
        foreach (var jobId in candidates)
        {
            var processingDeadline = DateTime.UtcNow.Add(ProcessingTimeout);
            var claimed = await context.Set<DeliveryOutbox>().Where(x => x.DeliveryOutboxId == jobId && x.Status == (int)DeliveryOutboxStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, (int)DeliveryOutboxStatus.Processing)
                    .SetProperty(x => x.NextAttemptAt, processingDeadline), ct);
            if (claimed == 0) continue;
            var job = await context.Set<DeliveryOutbox>().FirstAsync(x => x.DeliveryOutboxId == jobId, ct);
            try
            {
                var payload = JsonSerializer.Deserialize<ScheduleDeliveryPayload>(job.Payload, JsonOptions)
                    ?? throw new InvalidOperationException("Invalid schedule delivery payload.");
                if (job.Channel == (int)DeliveryChannel.Email)
                {
                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await email.SendEmailAsync(new EmailRequest { To = payload.Email, Subject = payload.Subject,
                        Body = payload.HtmlBody, TextBody = payload.TextBody, IsHtml = true,
                        MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>" }, ct);
                }
                else
                {
                    var notification = await context.Set<Notification>()
                        .FirstOrDefaultAsync(x => x.NotificationsId == payload.NotificationId, ct);
                    if (notification is null && payload.CreateNotificationAtDelivery)
                    {
                        notification = await context.Set<Notification>().FirstOrDefaultAsync(x =>
                            x.UserId == payload.UserId && x.Type == (int)NotificationType.Schedule &&
                            x.ReferenceId == payload.ReferenceId && x.IsRead != true, ct);
                        if (notification is null)
                        {
                            notification = new Notification
                            {
                                NotificationsId = payload.NotificationId,
                                UserId = payload.UserId
                            };
                            context.Set<Notification>().Add(notification);
                        }

                        notification.Type = (int)NotificationType.Schedule;
                        notification.Title = payload.NotificationTitle ?? "Meeting time reached";
                        notification.Content = payload.NotificationContent ?? "Your scheduled meeting is starting now.";
                        notification.ReferenceId = payload.ReferenceId;
                        notification.ReferenceType = "Schedule";
                        notification.Metadata = payload.Metadata;
                        notification.Revision = payload.Revision;
                        notification.IsRead = false;
                        notification.ReadAt = null;
                        notification.CreatedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync(ct);
                    }
                    if (notification is not null)
                    {
                        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
                        await sender.SendToUserAsync(payload.UserId, new NotificationDto
                        {
                            Id = notification.NotificationsId, Source = "Personal", NotificationId = notification.NotificationsId,
                            ReadTargetId = notification.NotificationsId, Type = (NotificationType)notification.Type,
                            Title = notification.Title, Content = notification.Content, ReferenceId = notification.ReferenceId,
                            ReferenceType = notification.ReferenceType, Metadata = notification.Metadata, Revision = notification.Revision,
                            IsRead = notification.IsRead ?? false, ReadAt = notification.ReadAt, CreatedAt = notification.CreatedAt
                        }, ct);
                    }
                    else
                    {
                        _logger.LogWarning("Notification {NotificationId} for schedule delivery {DeliveryKey} was not found; marking the delivery as completed.",
                            payload.NotificationId, job.DeliveryKey);
                    }
                }
                job.Status = (int)DeliveryOutboxStatus.Delivered; job.DeliveredAt = DateTime.UtcNow; job.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                job.AttemptCount++;
                job.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                if (job.AttemptCount > RetryDelays.Length)
                {
                    job.Status = (int)DeliveryOutboxStatus.DeadLettered;
                    _logger.LogError(ex, "Schedule delivery {DeliveryKey} dead-lettered after {Attempts} attempts.", job.DeliveryKey, job.AttemptCount);
                }
                else
                {
                    job.Status = (int)DeliveryOutboxStatus.Pending;
                    job.NextAttemptAt = DateTime.UtcNow.Add(RetryDelays[job.AttemptCount - 1]);
                    _logger.LogWarning(ex, "Schedule delivery {DeliveryKey} failed; attempt {Attempt} scheduled.", job.DeliveryKey, job.AttemptCount);
                }
            }
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureAcceptedSchedulesHaveStartDeliveries(
        IApplicationDbContext context, DateTime now, string frontendBaseUrl,
        IScheduleEmailRenderer emailRenderer, CancellationToken ct)
    {
        // Backfill schedules accepted before this feature was deployed. The
        // 24-hour lower bound prevents historical meetings from generating mail.
        var schedules = await context.Set<Schedule>().AsNoTracking()
            .Where(x => x.Status == ScheduleStatus.Scheduled &&
                x.AgreementStatus == ScheduleAgreementStatus.Accepted &&
                x.ScheduledAtUtc >= now.AddHours(-24))
            .OrderBy(x => x.ScheduledAtUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            var scheduleMessageId = await context.Set<Message>().AsNoTracking()
                .Where(x => x.ScheduleId == schedule.ScheduleId)
                .OrderByDescending(x => x.ScheduleEventSequence)
                .Select(x => (Guid?)x.MessagesId)
                .FirstOrDefaultAsync(ct);
            var scheduleUrl = $"{frontendBaseUrl}/messages?conversationId={schedule.ConversationId:D}" +
                (scheduleMessageId.HasValue ? $"&messageId={scheduleMessageId.Value:D}" : "");
            var participantRows = await context.Set<ConversationParticipant>().AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.ConversationsId == schedule.ConversationId &&
                    x.LeftAt == null && x.DeletedAt == null)
                .ToListAsync(ct);
            var participants = participantRows.GroupBy(x => x.UserId).Select(x => x.First());
            var existingKeys = await context.Set<DeliveryOutbox>().AsNoTracking()
                .Where(x => x.ScheduleId == schedule.ScheduleId && x.DeliveryKey.Contains(":start:"))
                .Select(x => x.DeliveryKey)
                .ToListAsync(ct);
            var keys = existingKeys.ToHashSet(StringComparer.Ordinal);

            foreach (var participant in participants)
            {
                var notificationId = Guid.NewGuid();
                var metadata = JsonSerializer.Serialize(new
                {
                    schemaVersion = 3,
                    scheduleId = schedule.ScheduleId,
                    conversationId = schedule.ConversationId,
                    scheduledAtUtc = schedule.ScheduledAtUtc,
                    agreementStatus = (int)schedule.AgreementStatus
                }, JsonOptions);
                var title = "Meeting time reached";
                var content = $"{schedule.Title} is starting now.";
                var email = emailRenderer.Render(ScheduleNotificationType.MeetingStarting,
                    new ScheduleEmailModel(participant.User.FullName, "GigBridge", false,
                        schedule.Title, FormatVietnamTime(schedule.ScheduledAtUtc), schedule.Details, null,
                        scheduleUrl,
                        schedule.MeetingStatus == MeetingProvisioningStatus.Ready ? schedule.MeetingJoinUri : null));

                foreach (var channel in new[] { DeliveryChannel.NotificationRealtime, DeliveryChannel.Email })
                {
                    var key = $"schedule:{schedule.ScheduleId}:start:{participant.UserId}:{(int)channel}";
                    if (!keys.Add(key)) continue;
                    var payload = JsonSerializer.Serialize(new ScheduleDeliveryPayload(
                        notificationId, participant.UserId, participant.User.Email,
                        email.Subject, email.HtmlBody, metadata,
                        true, title, content, schedule.ScheduleId, schedule.Version, email.TextBody), JsonOptions);
                    context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
                    {
                        DeliveryOutboxId = Guid.NewGuid(), DeliveryKey = key,
                        ScheduleId = schedule.ScheduleId, RecipientUserId = participant.UserId,
                        EventSequence = schedule.Version, Channel = (int)channel, Payload = payload,
                        Status = (int)DeliveryOutboxStatus.Pending,
                        NextAttemptAt = schedule.ScheduledAtUtc > now ? schedule.ScheduledAtUtc : now,
                        CreatedAt = now
                    });
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private static string FormatVietnamTime(DateTime utc)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        var value = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(value, zone).ToString("dd MMM yyyy, HH:mm 'ICT'");
    }
}
