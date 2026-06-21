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

    public DeliveryOutboxService(IServiceScopeFactory scopeFactory, ILogger<DeliveryOutboxService> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

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
                    await email.SendEmailAsync(new EmailRequest { To = payload.Email, Subject = payload.Subject, Body = payload.HtmlBody, IsHtml = true,
                        MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>" }, ct);
                }
                else
                {
                    var notification = await context.Set<Notification>().AsNoTracking().FirstOrDefaultAsync(x => x.NotificationsId == payload.NotificationId, ct);
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
}
