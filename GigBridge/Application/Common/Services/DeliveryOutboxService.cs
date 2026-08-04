using System.Net;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Options;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Common.Services;

public sealed class DeliveryOutboxService : BackgroundService
{
    internal static readonly TimeSpan DueDeliveryPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RealtimeMaxIdlePollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EmailMaxIdlePollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BackfillRetryInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(6)];
    private static readonly DeliveryChannel[] DeliveryChannels =
        [DeliveryChannel.NotificationRealtime, DeliveryChannel.Email];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
    private const long ScheduleStartBackfillLockKey = 4_440_759_050_001;
    private const string ScheduleStartBackfillOperation = "schedule-start-v1";
    private const string CompactedPayload = "{}";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryOutboxService> _logger;
    private readonly IScheduleEmailRenderer _emailRenderer;
    private readonly DeliveryOutboxOptions _options;
    private readonly string _frontendBaseUrl;

    public DeliveryOutboxService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveryOutboxService> logger,
        IConfiguration configuration,
        IScheduleEmailRenderer emailRenderer,
        IOptions<DeliveryOutboxOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _emailRenderer = emailRenderer;
        _options = options.Value;
        _frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backfill = _options.ScheduleStartBackfillEnabled
            ? RunScheduleStartBackfillAsync(stoppingToken)
            : Task.CompletedTask;

        await Task.WhenAll(
            RunChannelLoopAsync(DeliveryChannel.NotificationRealtime, stoppingToken),
            RunChannelLoopAsync(DeliveryChannel.Email, stoppingToken),
            RunMaintenanceLoopAsync(stoppingToken),
            backfill);
    }

    private async Task RunChannelLoopAsync(
        DeliveryChannel channel,
        CancellationToken stoppingToken)
    {
        var activeInterval = channel == DeliveryChannel.NotificationRealtime
            ? TimeSpan.FromMilliseconds(_options.RealtimePollMilliseconds)
            : TimeSpan.FromMilliseconds(_options.EmailPollMilliseconds);
        var maxIdleInterval = channel == DeliveryChannel.NotificationRealtime
            ? RealtimeMaxIdlePollInterval
            : EmailMaxIdlePollInterval;
        var idleInterval = activeInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedWork = false;
            try
            {
                processedWork = await ProcessChannelBatch(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delivery outbox {Channel} batch failed.", channel);
            }

            var delay = processedWork ? activeInterval : idleInterval;
            idleInterval = processedWork
                ? activeInterval
                : NextIdleInterval(idleInterval, maxIdleInterval);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task<bool> ProcessChannelBatch(
        DeliveryChannel channel,
        CancellationToken ct)
    {
        IReadOnlyList<DeliveryOutboxLease> leases;
        var now = DateTime.UtcNow;
        using (var claimScope = _scopeFactory.CreateScope())
        {
            var store = claimScope.ServiceProvider.GetRequiredService<IDeliveryOutboxStore>();
            leases = await store.ClaimDueAsync(
                channel,
                now,
                now.AddMinutes(_options.LeaseMinutes),
                _options.BatchSize,
                ct);
        }

        if (leases.Count == 0)
        {
            return false;
        }

        var concurrency = channel == DeliveryChannel.NotificationRealtime
            ? _options.RealtimeMaxConcurrency
            : _options.EmailMaxConcurrency;
        await Parallel.ForEachAsync(
            leases,
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = concurrency
            },
            async (lease, cancellationToken) =>
                await ProcessLeasedDeliveryAsync(lease, cancellationToken));
        return true;
    }

    private static TimeSpan NextIdleInterval(TimeSpan current, TimeSpan maximum) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, maximum.Ticks));

    private async Task ProcessLeasedDeliveryAsync(
        DeliveryOutboxLease lease,
        CancellationToken ct)
    {
        DeliveryOutbox? job = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            job = await context.Set<DeliveryOutbox>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.DeliveryOutboxId == lease.DeliveryOutboxId &&
                    x.Status == (int)DeliveryOutboxStatus.Processing &&
                    x.ClaimToken == lease.ClaimToken, ct);
            if (job is null)
            {
                return;
            }

            await DispatchDeliveryAsync(context, scope.ServiceProvider, job, ct);
            using var outcomeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var completed = await CompleteLeaseAsync(
                lease,
                DateTime.UtcNow,
                outcomeTimeout.Token);
            if (completed == 0)
            {
                _logger.LogInformation(
                    "Delivery {DeliveryKey} completed after lease ownership changed; the current state was preserved.",
                    job.DeliveryKey);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await TryReleaseLeaseOnShutdownAsync(lease);
            throw;
        }
        catch (Exception ex)
        {
            if (job is null)
            {
                _logger.LogError(ex,
                    "Delivery {DeliveryOutboxId} could not be loaded after it was claimed.",
                    lease.DeliveryOutboxId);
                return;
            }

            await FailLeaseAsync(lease, job, ex, ct);
        }
    }

    private async Task DispatchDeliveryAsync(
        IApplicationDbContext context,
        IServiceProvider services,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        if (job.ScheduleId is null)
        {
            await SendFinalContractEmailAsync(context, services, job, ct);
            return;
        }

        var payload = JsonSerializer.Deserialize<ScheduleDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid schedule delivery payload.");
        switch ((DeliveryChannel)job.Channel)
        {
            case DeliveryChannel.Email:
            {
                var email = services.GetRequiredService<IEmailService>();
                await email.SendEmailAsync(new EmailRequest
                {
                    To = payload.Email,
                    Subject = payload.Subject,
                    Body = payload.HtmlBody,
                    TextBody = payload.TextBody,
                    IsHtml = true,
                    IdempotencyKey = job.DeliveryKey,
                    MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>"
                }, ct);
                break;
            }
            case DeliveryChannel.NotificationRealtime:
                await SendScheduleNotificationAsync(context, services, job, payload, ct);
                break;
            default:
                throw new InvalidOperationException($"Unsupported delivery channel {job.Channel}.");
        }
    }

    private async Task SendScheduleNotificationAsync(
        IApplicationDbContext context,
        IServiceProvider services,
        DeliveryOutbox job,
        ScheduleDeliveryPayload payload,
        CancellationToken ct)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(x => x.NotificationsId == payload.NotificationId, ct);
        if (notification is null && payload.CreateNotificationAtDelivery)
        {
            notification = await context.Set<Notification>().FirstOrDefaultAsync(x =>
                x.UserId == payload.UserId &&
                x.Type == (int)NotificationType.Schedule &&
                x.ReferenceId == payload.ReferenceId &&
                x.IsRead != true, ct);
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

        if (notification is null)
        {
            _logger.LogWarning(
                "Notification {NotificationId} for schedule delivery {DeliveryKey} was not found; marking the delivery as completed.",
                payload.NotificationId,
                job.DeliveryKey);
            return;
        }

        var sender = services.GetRequiredService<INotificationSender>();
        await sender.SendToUserAsync(payload.UserId, new NotificationDto
        {
            Id = notification.NotificationsId,
            Source = "Personal",
            NotificationId = notification.NotificationsId,
            ReadTargetId = notification.NotificationsId,
            Type = (NotificationType)notification.Type,
            Title = notification.Title,
            Content = notification.Content,
            ReferenceId = notification.ReferenceId,
            ReferenceType = notification.ReferenceType,
            Metadata = notification.Metadata,
            Revision = notification.Revision,
            IsRead = notification.IsRead ?? false,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        }, ct);
    }

    private async Task<int> CompleteLeaseAsync(
        DeliveryOutboxLease lease,
        DateTime deliveredAt,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await context.Set<DeliveryOutbox>()
            .Where(x =>
                x.DeliveryOutboxId == lease.DeliveryOutboxId &&
                x.Status == (int)DeliveryOutboxStatus.Processing &&
                x.ClaimToken == lease.ClaimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, (int)DeliveryOutboxStatus.Delivered)
                .SetProperty(x => x.DeliveredAt, deliveredAt)
                .SetProperty(x => x.ClaimToken, (Guid?)null)
                .SetProperty(x => x.LastError, (string?)null), ct);
    }

    private async Task FailLeaseAsync(
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        Exception exception,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var attempt = job.AttemptCount + 1;
        var error = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
        if (error.Length > 2_000)
        {
            error = error[..2_000];
        }

        var deadLetter = IsPermanentFailure(exception) || attempt > RetryDelays.Length;
        var nextAttemptAt = deadLetter
            ? now
            : now.Add(RetryDelays[attempt - 1]);
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var updated = await context.Set<DeliveryOutbox>()
            .Where(x =>
                x.DeliveryOutboxId == lease.DeliveryOutboxId &&
                x.Status == (int)DeliveryOutboxStatus.Processing &&
                x.ClaimToken == lease.ClaimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, deadLetter
                    ? (int)DeliveryOutboxStatus.DeadLettered
                    : (int)DeliveryOutboxStatus.Pending)
                .SetProperty(x => x.AttemptCount, attempt)
                .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
                .SetProperty(x => x.ClaimToken, (Guid?)null)
                .SetProperty(x => x.LastError, error), ct);
        if (updated == 0)
        {
            _logger.LogInformation(
                "Delivery {DeliveryKey} failed after lease ownership changed; the current state was preserved.",
                job.DeliveryKey);
            return;
        }

        if (deadLetter)
        {
            _logger.LogError(exception,
                "Delivery {DeliveryKey} dead-lettered after {Attempts} attempts.",
                job.DeliveryKey,
                attempt);
        }
        else
        {
            _logger.LogWarning(exception,
                "Delivery {DeliveryKey} failed; attempt {Attempt} scheduled for {NextAttemptAt}.",
                job.DeliveryKey,
                attempt,
                nextAttemptAt);
        }
    }

    private async Task TryReleaseLeaseOnShutdownAsync(DeliveryOutboxLease lease)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            await context.Set<DeliveryOutbox>()
                .Where(x =>
                    x.DeliveryOutboxId == lease.DeliveryOutboxId &&
                    x.Status == (int)DeliveryOutboxStatus.Processing &&
                    x.ClaimToken == lease.ClaimToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, (int)DeliveryOutboxStatus.Pending)
                    .SetProperty(x => x.NextAttemptAt, DateTime.UtcNow)
                    .SetProperty(x => x.ClaimToken, (Guid?)null), timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Delivery {DeliveryOutboxId} lease could not be released during shutdown.",
                lease.DeliveryOutboxId);
        }
    }

    private async Task RunMaintenanceLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.LeaseRecoveryIntervalSeconds);
        var nextRetentionAt = DateTime.MinValue;
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                await RecoverExpiredLeasesAsync(now, stoppingToken);
                if (now >= nextRetentionAt)
                {
                    await CompactDeliveredPayloadsAsync(now, stoppingToken);
                    nextRetentionAt = now.AddMinutes(_options.RetentionIntervalMinutes);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delivery outbox maintenance failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RecoverExpiredLeasesAsync(DateTime now, CancellationToken ct)
    {
        var recovered = 0;
        foreach (var channel in DeliveryChannels)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            recovered += await context.Set<DeliveryOutbox>()
                .Where(x =>
                    x.Channel == (int)channel &&
                    x.Status == (int)DeliveryOutboxStatus.Processing &&
                    x.NextAttemptAt <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, x => x.AttemptCount >= RetryDelays.Length
                        ? (int)DeliveryOutboxStatus.DeadLettered
                        : (int)DeliveryOutboxStatus.Pending)
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.NextAttemptAt, now)
                    .SetProperty(x => x.ClaimToken, (Guid?)null)
                    .SetProperty(x => x.LastError, "The delivery processing lease expired."), ct);
        }

        if (recovered > 0)
        {
            _logger.LogWarning("Recovered {Count} expired delivery leases.", recovered);
        }
    }

    private async Task CompactDeliveredPayloadsAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-_options.DeliveredPayloadRetentionDays);
        var total = 0;
        for (var batch = 0; batch < 10; batch++)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var ids = await context.Set<DeliveryOutbox>()
                .AsNoTracking()
                .Where(x =>
                    x.Status == (int)DeliveryOutboxStatus.Delivered &&
                    x.DeliveredAt <= cutoff &&
                    x.Payload != CompactedPayload)
                .OrderBy(x => x.DeliveredAt)
                .ThenBy(x => x.DeliveryOutboxId)
                .Select(x => x.DeliveryOutboxId)
                .Take(_options.RetentionBatchSize)
                .ToListAsync(ct);
            if (ids.Count == 0)
            {
                break;
            }

            total += await context.Set<DeliveryOutbox>()
                .Where(x =>
                    ids.Contains(x.DeliveryOutboxId) &&
                    x.Status == (int)DeliveryOutboxStatus.Delivered &&
                    x.DeliveredAt <= cutoff)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Payload, CompactedPayload)
                    .SetProperty(x => x.LastError, (string?)null), ct);
            if (ids.Count < _options.RetentionBatchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            _logger.LogInformation(
                "Compacted payloads for {Count} delivered outbox rows while preserving delivery keys.",
                total);
        }
    }

    private async Task RunScheduleStartBackfillAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessScheduleStartBackfillPageAsync(stoppingToken))
                {
                    _logger.LogInformation("Meeting-start delivery backfill is complete.");
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meeting-start delivery backfill page failed.");
                await Task.Delay(BackfillRetryInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessScheduleStartBackfillPageAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IDeliveryOutboxStore>();
        await using var transaction = await context.BeginTransactionAsync(ct);
        await transaction.AcquireTransactionLockAsync(ScheduleStartBackfillLockKey, ct);

        var now = DateTime.UtcNow;
        var state = await context.Set<DeliveryOutboxMaintenanceState>()
            .FirstOrDefaultAsync(x => x.Operation == ScheduleStartBackfillOperation, ct);
        if (state is null)
        {
            state = new DeliveryOutboxMaintenanceState
            {
                Operation = ScheduleStartBackfillOperation,
                WindowStartAt = now.AddHours(-24),
                UpdatedAt = now
            };
            context.Set<DeliveryOutboxMaintenanceState>().Add(state);
        }
        else if (state.CompletedAt.HasValue)
        {
            await transaction.CommitAsync(ct);
            return false;
        }

        var schedules = await store.LoadScheduleStartBackfillPageAsync(
            state.WindowStartAt,
            state.LastScheduledAtUtc,
            state.LastScheduleId,
            _options.BackfillPageSize,
            ct);
        if (schedules.Count == 0)
        {
            state.CompletedAt = now;
            state.UpdatedAt = now;
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return false;
        }

        var scheduleIds = schedules.Select(x => x.ScheduleId).ToArray();
        var conversationIds = schedules.Select(x => x.ConversationId).Distinct().ToArray();
        var messageRows = await context.Set<Message>()
            .AsNoTracking()
            .Where(x => x.ScheduleId.HasValue && scheduleIds.Contains(x.ScheduleId.Value))
            .Select(x => new BackfillMessage(
                x.ScheduleId!.Value,
                x.MessagesId,
                x.ScheduleEventSequence ?? 0))
            .ToListAsync(ct);
        var latestMessages = messageRows
            .GroupBy(x => x.ScheduleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.EventSequence)
                    .ThenByDescending(x => x.MessageId)
                    .First().MessageId);
        var participants = await context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(x =>
                conversationIds.Contains(x.ConversationsId) &&
                x.LeftAt == null &&
                x.DeletedAt == null)
            .Select(x => new BackfillParticipant(
                x.ConversationsId,
                x.UserId,
                x.User.FullName,
                x.User.Email))
            .ToListAsync(ct);
        var participantsByConversation = participants
            .GroupBy(x => x.ConversationId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var existingRows = await context.Set<DeliveryOutbox>()
            .AsNoTracking()
            .Where(x =>
                x.ScheduleId.HasValue &&
                scheduleIds.Contains(x.ScheduleId.Value) &&
                x.DeliveryKey.Contains(":start:"))
            .Select(x => new BackfillExistingDelivery(x.DeliveryKey, x.Payload))
            .ToListAsync(ct);
        var existingByKey = existingRows.ToDictionary(x => x.DeliveryKey, StringComparer.Ordinal);
        var inserts = new List<ScheduleStartDeliveryInsert>();

        foreach (var schedule in schedules)
        {
            if (!participantsByConversation.TryGetValue(schedule.ConversationId, out var scheduleParticipants))
            {
                continue;
            }

            latestMessages.TryGetValue(schedule.ScheduleId, out var scheduleMessageId);
            var scheduleUrl = $"{_frontendBaseUrl}/messages?conversationId={schedule.ConversationId:D}" +
                (scheduleMessageId != Guid.Empty ? $"&messageId={scheduleMessageId:D}" : string.Empty);
            var formattedTime = FormatVietnamTime(schedule.ScheduledAtUtc);
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

            foreach (var participant in scheduleParticipants)
            {
                var realtimeKey = StartDeliveryKey(
                    schedule.ScheduleId,
                    participant.UserId,
                    DeliveryChannel.NotificationRealtime);
                var emailKey = StartDeliveryKey(
                    schedule.ScheduleId,
                    participant.UserId,
                    DeliveryChannel.Email);
                var hasRealtime = existingByKey.TryGetValue(realtimeKey, out var existingRealtime);
                var hasEmail = existingByKey.TryGetValue(emailKey, out var existingEmail);
                if (hasRealtime && hasEmail)
                {
                    continue;
                }

                var notificationId = TryGetNotificationId(existingRealtime?.Payload) ??
                    TryGetNotificationId(existingEmail?.Payload) ??
                    Guid.NewGuid();
                var email = _emailRenderer.Render(
                    ScheduleNotificationType.MeetingStarting,
                    new ScheduleEmailModel(
                        participant.FullName,
                        "GigBridge",
                        false,
                        schedule.Title,
                        formattedTime,
                        schedule.Details,
                        null,
                        scheduleUrl,
                        schedule.MeetingStatus == MeetingProvisioningStatus.Ready
                            ? schedule.MeetingJoinUri
                            : null));

                if (!hasRealtime)
                {
                    inserts.Add(CreateStartDelivery(
                        schedule,
                        participant,
                        DeliveryChannel.NotificationRealtime,
                        realtimeKey,
                        notificationId,
                        metadata,
                        title,
                        content,
                        email,
                        now));
                }

                if (!hasEmail)
                {
                    inserts.Add(CreateStartDelivery(
                        schedule,
                        participant,
                        DeliveryChannel.Email,
                        emailKey,
                        notificationId,
                        metadata,
                        title,
                        content,
                        email,
                        now));
                }
            }
        }

        var inserted = await store.InsertScheduleStartDeliveriesAsync(inserts, ct);
        var last = schedules[^1];
        state.LastScheduledAtUtc = last.ScheduledAtUtc;
        state.LastScheduleId = last.ScheduleId;
        state.UpdatedAt = now;
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _logger.LogInformation(
            "Meeting-start delivery backfill scanned {ScheduleCount} schedules and inserted {InsertedCount} missing deliveries.",
            schedules.Count,
            inserted);
        return true;
    }

    internal static IQueryable<DeliveryOutbox> DueDeliveriesForChannel(
        IQueryable<DeliveryOutbox> deliveries,
        DeliveryChannel channel,
        DateTime now) =>
        deliveries
            .Where(x =>
                x.Channel == (int)channel &&
                x.Status == (int)DeliveryOutboxStatus.Pending &&
                x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.DeliveryOutboxId);

    private static ScheduleStartDeliveryInsert CreateStartDelivery(
        ScheduleStartBackfillSchedule schedule,
        BackfillParticipant participant,
        DeliveryChannel channel,
        string deliveryKey,
        Guid notificationId,
        string metadata,
        string title,
        string content,
        RenderedScheduleEmail email,
        DateTime now)
    {
        var payload = JsonSerializer.Serialize(new ScheduleDeliveryPayload(
            notificationId,
            participant.UserId,
            participant.Email,
            email.Subject,
            email.HtmlBody,
            metadata,
            true,
            title,
            content,
            schedule.ScheduleId,
            schedule.Version,
            email.TextBody), JsonOptions);
        return new ScheduleStartDeliveryInsert(
            new DeliveryOutbox
            {
                DeliveryOutboxId = Guid.NewGuid(),
                DeliveryKey = deliveryKey,
                ScheduleId = schedule.ScheduleId,
                RecipientUserId = participant.UserId,
                EventSequence = schedule.Version,
                Channel = (int)channel,
                Payload = payload,
                Status = (int)DeliveryOutboxStatus.Pending,
                NextAttemptAt = schedule.ScheduledAtUtc > now ? schedule.ScheduledAtUtc : now,
                CreatedAt = now
            },
            schedule.ScheduledAtUtc);
    }

    private static async Task SendFinalContractEmailAsync(
        IApplicationDbContext context,
        IServiceProvider services,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        if (job.Channel != (int)DeliveryChannel.Email)
        {
            throw new InvalidOperationException("ESign outbox deliveries only support email.");
        }

        var payload = JsonSerializer.Deserialize<ContractEsignDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid ESign contract delivery payload.");
        var document = await context.Set<EsignDocument>()
            .AsNoTracking()
            .Where(item => item.EsignDocumentsId == payload.DocumentId)
            .Select(item => new FinalContractArtifact(
                item.Status,
                item.DocumentCode,
                item.FinalizedDocumentContent,
                item.FinalizedDocumentFileName,
                item.FinalizedDocumentMimeType))
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The finalized ESign document no longer exists.");
        if (document.Status != (int)ESignDocumentStatus.FullySigned ||
            document.Content is not { Length: > 0 } ||
            string.IsNullOrWhiteSpace(document.FileName) ||
            string.IsNullOrWhiteSpace(document.MimeType))
        {
            throw new InvalidOperationException("The finalized ESign DOCX artifact is not available.");
        }

        var recipientName = WebUtility.HtmlEncode(payload.RecipientName);
        var contractTitle = WebUtility.HtmlEncode(payload.ContractTitle);
        var code = WebUtility.HtmlEncode(document.DocumentCode);
        var email = services.GetRequiredService<IEmailService>();
        await email.SendEmailAsync(new EmailRequest
        {
            To = payload.Email,
            Subject = $"[GigBridge] Hợp đồng {document.DocumentCode} đã hoàn tất",
            Body = $"<p>Xin chào {recipientName},</p><p>Hợp đồng <strong>{contractTitle}</strong> ({code}) đã được Client và Freelancer ký đầy đủ.</p><p>Bản DOCX hoàn tất được đính kèm email này.</p>",
            TextBody = $"Xin chào {payload.RecipientName}, hợp đồng {payload.ContractTitle} ({document.DocumentCode}) đã được ký đầy đủ. Bản DOCX hoàn tất được đính kèm email này.",
            IsHtml = true,
            IdempotencyKey = job.DeliveryKey,
            MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>",
            ByteAttachments =
            [
                new EmailByteAttachment(document.FileName, document.Content, document.MimeType)
            ]
        }, ct);
    }

    private static bool IsPermanentFailure(Exception exception) =>
        exception is JsonException or InvalidOperationException;

    private static string StartDeliveryKey(
        Guid scheduleId,
        Guid userId,
        DeliveryChannel channel) =>
        $"schedule:{scheduleId}:start:{userId}:{(int)channel}";

    private static Guid? TryGetNotificationId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduleDeliveryPayload>(payload, JsonOptions)?.NotificationId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatVietnamTime(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(value, VietnamTimeZone)
            .ToString("dd MMM yyyy, HH:mm 'ICT'");
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private sealed record BackfillMessage(Guid ScheduleId, Guid MessageId, int EventSequence);
    private sealed record BackfillParticipant(Guid ConversationId, Guid UserId, string FullName, string Email);
    private sealed record BackfillExistingDelivery(string DeliveryKey, string Payload);
    private sealed record FinalContractArtifact(
        int Status,
        string DocumentCode,
        byte[]? Content,
        string? FileName,
        string? MimeType);
}
