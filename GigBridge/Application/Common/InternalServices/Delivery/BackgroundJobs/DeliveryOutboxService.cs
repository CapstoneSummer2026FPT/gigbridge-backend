using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Chat.Models;
using Application.Common.InternalServices.Chat.Services;
using Application.Common.InternalServices.ESign.Models;
using Application.Common.InternalServices.ESign.Services;
using Application.Common.InternalServices.Notifications.Models;
using Application.Common.InternalServices.Realtime.Models;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Delivery.Interfaces;
using Application.Common.InternalServices.Delivery.Models;
using Application.Common.InternalServices.WorkSignals.Interfaces;
using Application.Common.InternalServices.WorkSignals.Models;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.Options;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Common;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Milestones.Email;
using Application.Common.InternalServices.Contracts.Models;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Application.Features.ESign.Common.Internal;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
using Domain.Enums.ESign;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Common.Models.Email;

namespace Application.Common.InternalServices.Delivery.BackgroundJobs;

public sealed class DeliveryOutboxService : BackgroundService
{
    internal static readonly TimeSpan DueDeliveryPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RealtimeMaxIdlePollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EmailMaxIdlePollInterval = TimeSpan.FromMinutes(5);
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
    private readonly ISignedEmailRenderer _signedEmailRenderer;
    private readonly IMilestoneSubmissionEmailRenderer _milestoneSubmissionEmailRenderer;
    private readonly DeliveryOutboxOptions _options;
    private readonly WorkSignalOptions _workSignalOptions;
    private readonly IWorkSignalSource _workSignal;
    private readonly DeliveryOutboxDatabaseGate _databaseGate;
    private readonly string _frontendBaseUrl;

    public DeliveryOutboxService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveryOutboxService> logger,
        IConfiguration configuration,
        IScheduleEmailRenderer emailRenderer,
        ISignedEmailRenderer signedEmailRenderer,
        IMilestoneSubmissionEmailRenderer milestoneSubmissionEmailRenderer,
        IOptions<DeliveryOutboxOptions> options,
        IOptions<WorkSignalOptions> workSignalOptions,
        [FromKeyedServices(WorkSignalChannels.DeliveryOutbox)] IWorkSignalSource workSignal)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _emailRenderer = emailRenderer;
        _signedEmailRenderer = signedEmailRenderer;
        _milestoneSubmissionEmailRenderer = milestoneSubmissionEmailRenderer;
        _options = options.Value;
        _workSignalOptions = workSignalOptions.Value;
        _workSignal = workSignal;
        _databaseGate = new DeliveryOutboxDatabaseGate(_options.MaxConcurrentDbConnections);
        _frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public override void Dispose()
    {
        _databaseGate.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Delivery outbox worker started.");
        await RecoverReadyFinalContractEmailsAsync(stoppingToken);

        var backfill = _options.ScheduleStartBackfillEnabled
            ? RunScheduleStartBackfillAsync(stoppingToken)
            : Task.CompletedTask;

        await Task.WhenAll(
            RunChannelLoopAsync(DeliveryChannel.NotificationRealtime, stoppingToken),
            RunChannelLoopAsync(DeliveryChannel.Email, stoppingToken),
            RunMaintenanceLoopAsync(stoppingToken),
            backfill);
    }

    private async Task RecoverReadyFinalContractEmailsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var failedDeliveries = await context.Set<DeliveryOutbox>()
                .Where(delivery =>
                    delivery.ScheduleId == null &&
                    delivery.Channel == (int)DeliveryChannel.Email &&
                    delivery.Status == (int)DeliveryOutboxStatus.DeadLettered &&
                    delivery.DeliveryKey.StartsWith("esign:") &&
                    delivery.LastError == "The finalized ESign PDF artifact is not available.")
                .ToListAsync(cancellationToken);
            if (failedDeliveries.Count == 0)
            {
                return;
            }

            var deliveryDocuments = failedDeliveries
                .Select(delivery => new
                {
                    Delivery = delivery,
                    Payload = TryReadContractDeliveryPayload(delivery.Payload)
                })
                .Where(item => item.Payload is not null)
                .ToList();
            var documentIds = deliveryDocuments
                .Select(item => item.Payload!.DocumentId)
                .Distinct()
                .ToArray();
            var documents = await context.Set<EsignDocument>()
                .AsNoTracking()
                .Where(document => documentIds.Contains(document.EsignDocumentsId))
                .ToListAsync(cancellationToken);
            var readyDocumentIds = documents
                .Where(IsFinalContractPdfReady)
                .Select(document => document.EsignDocumentsId)
                .ToHashSet();
            var now = DateTime.UtcNow;

            foreach (var item in deliveryDocuments.Where(item =>
                         readyDocumentIds.Contains(item.Payload!.DocumentId)))
            {
                item.Delivery.Status = (int)DeliveryOutboxStatus.Pending;
                item.Delivery.AttemptCount = 0;
                item.Delivery.NextAttemptAt = now;
                item.Delivery.ClaimToken = null;
                item.Delivery.LastError = null;
            }

            var recoveredCount = deliveryDocuments.Count(item =>
                readyDocumentIds.Contains(item.Payload!.DocumentId));
            if (recoveredCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Requeued {Count} finalized contract email deliveries.",
                    recoveredCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Finalized contract email recovery could not be completed at startup.");
        }
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

            try
            {
                if (processedWork)
                {
                    // A short breather between consecutive batches, not the idle wait — keep this
                    // even when the work signal is enabled, so a burst of work doesn't hammer the
                    // claim query back-to-back with no throttle at all.
                    await Task.Delay(activeInterval, stoppingToken);
                    idleInterval = activeInterval;
                    continue;
                }

                if (_workSignalOptions.Enabled)
                {
                    var deadline = await ResolveIdleDeadlineAsync(channel, activeInterval, maxIdleInterval, stoppingToken);
                    await _workSignal.WaitAsync(deadline, stoppingToken);
                    idleInterval = activeInterval;
                }
                else
                {
                    await Task.Delay(idleInterval, stoppingToken);
                    idleInterval = NextIdleInterval(idleInterval, maxIdleInterval);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Deadline for the idle wait: the next row's <c>NextAttemptAt</c> if one exists and it's
    /// sooner than <paramref name="maxIdleInterval"/> from now, otherwise the max-idle ceiling.
    /// Cheap index-only scan, run only when a batch found nothing to claim. This is what lets
    /// future-dated rows (retry backoff, scheduled reminders) wake the worker exactly on time
    /// instead of only on the next NOTIFY or the batch-idle ceiling — a naive "NOTIFY + long
    /// fallback" design would regress latency for those rows compared to the old fixed-interval
    /// polling.
    ///
    /// Clamped to never return sooner than <c>UtcNow + activeInterval</c>: a row can be Pending
    /// with an overdue NextAttemptAt while another instance holds it via
    /// <c>FOR UPDATE SKIP LOCKED</c> mid-claim (not yet committed to Processing) — this read sees
    /// the last-committed Pending/overdue state under READ COMMITTED, so without the clamp the
    /// resolved deadline would already be in the past, <c>WaitAsync</c> would return immediately,
    /// and the loop would spin against the database with no delay at all until the other instance
    /// finishes claiming.
    /// </summary>
    private async Task<DateTime> ResolveIdleDeadlineAsync(
        DeliveryChannel channel,
        TimeSpan activeInterval,
        TimeSpan maxIdleInterval,
        CancellationToken ct)
    {
        var ceiling = DateTime.UtcNow.Add(maxIdleInterval);
        var earliestAllowed = DateTime.UtcNow.Add(activeInterval);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var nextDueAt = await context.Set<DeliveryOutbox>()
                .Where(x => x.Channel == (int)channel && x.Status == (int)DeliveryOutboxStatus.Pending)
                .OrderBy(x => x.NextAttemptAt)
                .Select(x => (DateTime?)x.NextAttemptAt)
                .FirstOrDefaultAsync(ct);
            var deadline = nextDueAt.HasValue && nextDueAt.Value < ceiling ? nextDueAt.Value : ceiling;
            return deadline < earliestAllowed ? earliestAllowed : deadline;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to resolve the idle deadline for {Channel}; falling back to the max idle interval.",
                channel);
            return ceiling;
        }
    }

    internal async Task<bool> ProcessChannelBatch(
        DeliveryChannel channel,
        CancellationToken ct)
    {
        // Phase 1: finish all database preparation before network dispatch begins.
        var batch = await ClaimAndPrepareBatchAsync(channel, ct);
        if (batch.Leases.Count == 0)
        {
            return false;
        }

        IReadOnlyList<DeliveryOutcome> dispatchedOutcomes;
        try
        {
            // Phase 2: these scoped services perform network I/O only. No
            // IApplicationDbContext is resolved while deliveries run in parallel.
            dispatchedOutcomes = await DispatchBatchAsync(
                batch.Deliveries,
                channel == DeliveryChannel.NotificationRealtime
                    ? _options.RealtimeMaxConcurrency
                    : _options.EmailMaxConcurrency,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await ReleaseLeasesAsync(batch.Leases);
            throw;
        }

        var outcomes = batch.PreparationOutcomes
            .Concat(dispatchedOutcomes)
            .ToArray();
        // Phase 3: serialize outcome persistence through one database scope.
        await PersistOutcomesAsync(outcomes, ct);
        return true;
    }

    private async Task<PreparedDeliveryBatch> ClaimAndPrepareBatchAsync(
        DeliveryChannel channel,
        CancellationToken ct)
    {
        IReadOnlyList<DeliveryOutboxLease> leases = [];

        try
        {
            return await _databaseGate.RunAsync(async () =>
            {
                var now = DateTime.UtcNow;
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IDeliveryOutboxStore>();
                leases = await store.ClaimDueAsync(
                    channel,
                    now,
                    now.AddMinutes(_options.LeaseMinutes),
                    _options.BatchSize,
                    ct);
                if (leases.Count == 0)
                {
                    return PreparedDeliveryBatch.Empty;
                }

                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var leaseById = leases.ToDictionary(x => x.DeliveryOutboxId);
                var deliveryIds = leaseById.Keys.ToArray();
                var jobs = await context.Set<DeliveryOutbox>()
                    .AsNoTracking()
                    .Where(x =>
                        deliveryIds.Contains(x.DeliveryOutboxId) &&
                        x.Status == (int)DeliveryOutboxStatus.Processing)
                    .ToListAsync(ct);
                var deliveries = new List<PreparedDelivery>(jobs.Count);
                var outcomes = new List<DeliveryOutcome>();

                foreach (var job in jobs)
                {
                    if (!leaseById.TryGetValue(job.DeliveryOutboxId, out var lease) ||
                        job.ClaimToken != lease.ClaimToken)
                    {
                        continue;
                    }

                    try
                    {
                        var prepared = await PrepareDeliveryAsync(context, lease, job, ct);
                        if (prepared is null)
                        {
                            outcomes.Add(DeliveryOutcome.Delivered(
                                lease,
                                job.DeliveryKey,
                                job.AttemptCount,
                                DateTime.UtcNow));
                        }
                        else
                        {
                            deliveries.Add(prepared);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        outcomes.Add(FailedOutcome(lease, job.DeliveryKey, job.AttemptCount, ex));
                    }
                }

                await context.SaveChangesAsync(ct);
                return new PreparedDeliveryBatch(leases, deliveries, outcomes);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await ReleaseLeasesAsync(leases);
            throw;
        }
        catch
        {
            // No network delivery has started, so every claimed item can be made
            // immediately available instead of waiting for lease recovery.
            await ReleaseLeasesAsync(leases);
            throw;
        }
    }

    private async Task<PreparedDelivery?> PrepareDeliveryAsync(
        IApplicationDbContext context,
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        if (job.ScheduleId is null)
        {
            var deliveryType = (DeliveryOutboxType)(job.DeliveryType ?? (int)DeliveryOutboxType.FinalContractEmail);
            return deliveryType switch
            {
                DeliveryOutboxType.MilestoneSubmission =>
                    await PrepareMilestoneSubmissionEmailAsync(context, lease, job, ct),
                DeliveryOutboxType.GenericNotification =>
                    await PrepareGenericNotificationAsync(context, lease, job, ct),
                DeliveryOutboxType.ESignDocumentRevision =>
                    PrepareESignDocumentRevision(lease, job),
                DeliveryOutboxType.NotificationStateRevision =>
                    PrepareRevisionEvent<NotificationStateChangedPayload>(lease, job, RealtimeRevisionEvents.NotificationStateChanged),
                DeliveryOutboxType.ConversationInboxRevision =>
                    PrepareRevisionEvent<ConversationInboxRevisionChangedPayload>(lease, job, RealtimeRevisionEvents.ConversationInboxRevisionChanged),
                DeliveryOutboxType.ProjectReceiptRevision =>
                    PrepareRevisionEvent<ProjectReceiptRevisionChangedPayload>(lease, job, RealtimeRevisionEvents.ProjectReceiptRevisionChanged),
                _ => await PrepareFinalContractEmailAsync(context, lease, job, ct)
            };
        }

        var payload = JsonSerializer.Deserialize<ScheduleDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid schedule delivery payload.");
        switch ((DeliveryChannel)job.Channel)
        {
            case DeliveryChannel.Email:
                {
                    return PreparedDelivery.ForEmail(
                        lease,
                        job.DeliveryKey,
                        job.AttemptCount,
                        new EmailRequest
                        {
                            To = payload.Email,
                            Subject = payload.Subject,
                            Body = payload.HtmlBody,
                            TextBody = payload.TextBody,
                            IsHtml = true,
                            IdempotencyKey = job.DeliveryKey,
                            MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>"
                        });
                }
            case DeliveryChannel.NotificationRealtime:
                return await PrepareScheduledNotificationAsync(context, lease, job, payload, ct);
            default:
                throw new InvalidOperationException($"Unsupported delivery channel {job.Channel}.");
        }
    }

    private static PreparedDelivery PrepareESignDocumentRevision(
        DeliveryOutboxLease lease,
        DeliveryOutbox job)
    {
        if (job.Channel != (int)DeliveryChannel.NotificationRealtime)
        {
            throw new InvalidOperationException("ESign revision outbox deliveries require the realtime channel.");
        }

        var payload = JsonSerializer.Deserialize<ESignDocumentRevisionDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid ESign document revision delivery payload.");
        if (payload.Revision != job.EventSequence ||
            payload.ChangeKind is not (ESignDocumentRevision.UpsertChangeKind or ESignDocumentRevision.DeletedChangeKind))
        {
            throw new JsonException("Invalid ESign document revision sequence or change kind.");
        }

        return PreparedDelivery.ForRealtimeEvent(
            lease,
            job.DeliveryKey,
            job.AttemptCount,
            job.RecipientUserId,
            ESignDocumentRevision.ChangedEventName,
            payload);
    }

    private static PreparedDelivery PrepareRevisionEvent<TPayload>(
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        string eventName)
    {
        if (job.Channel != (int)DeliveryChannel.NotificationRealtime)
            throw new InvalidOperationException("Revision outbox deliveries require the realtime channel.");
        var payload = JsonSerializer.Deserialize<TPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid realtime revision payload.");
        return PreparedDelivery.ForRealtimeEvent(
            lease, job.DeliveryKey, job.AttemptCount, job.RecipientUserId, eventName, payload);
    }

    private async Task<PreparedDelivery?> PrepareScheduledNotificationAsync(
        IApplicationDbContext context,
        DeliveryOutboxLease lease,
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
        }

        if (notification is null)
        {
            _logger.LogWarning(
                "Notification {NotificationId} for schedule delivery {DeliveryKey} was not found; marking the delivery as completed.",
                payload.NotificationId,
                job.DeliveryKey);
            return null;
        }

        return PreparedDelivery.ForNotification(
            lease,
            job.DeliveryKey,
            job.AttemptCount,
            payload.UserId,
            ToNotificationDto(notification));
    }

    private async Task<PreparedDelivery?> PrepareGenericNotificationAsync(
        IApplicationDbContext context,
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<GenericNotificationDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid generic notification delivery payload.");

        if (payload.NotificationId.HasValue)
        {
            var notification = await context.Set<Notification>()
                .FirstOrDefaultAsync(x => x.NotificationsId == payload.NotificationId.Value, ct);
            if (notification is null)
            {
                _logger.LogWarning(
                    "Notification {NotificationId} for delivery {DeliveryKey} was not found; marking the delivery as completed.",
                    payload.NotificationId,
                    job.DeliveryKey);
                return null;
            }

            return PreparedDelivery.ForNotification(
                lease, job.DeliveryKey, job.AttemptCount, payload.UserId, ToNotificationDto(notification));
        }

        if (payload.BroadcastNotificationRecipientId.HasValue)
        {
            var recipient = await context.Set<BroadcastNotificationRecipient>()
                .Include(x => x.BroadcastNotification)
                .FirstOrDefaultAsync(
                    x => x.BroadcastNotificationRecipientId == payload.BroadcastNotificationRecipientId.Value, ct);
            if (recipient is null)
            {
                _logger.LogWarning(
                    "Broadcast notification recipient {RecipientId} for delivery {DeliveryKey} was not found; marking the delivery as completed.",
                    payload.BroadcastNotificationRecipientId,
                    job.DeliveryKey);
                return null;
            }

            return PreparedDelivery.ForNotification(
                lease, job.DeliveryKey, job.AttemptCount, payload.UserId,
                ToNotificationDto(recipient.BroadcastNotification, recipient));
        }

        throw new InvalidOperationException(
            $"Generic notification delivery {job.DeliveryKey} has neither a notification nor a broadcast recipient reference.");
    }

    private async Task<IReadOnlyList<DeliveryOutcome>> DispatchBatchAsync(
        IReadOnlyList<PreparedDelivery> deliveries,
        int concurrency,
        CancellationToken ct)
    {
        if (deliveries.Count == 0)
        {
            return [];
        }

        using var gate = new SemaphoreSlim(concurrency);
        var tasks = deliveries.Select(async delivery =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await DispatchPreparedDeliveryAsync(delivery, ct);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    private async Task<DeliveryOutcome> DispatchPreparedDeliveryAsync(
        PreparedDelivery delivery,
        CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            if (delivery.Email is not null)
            {
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await email.SendEmailAsync(delivery.Email, ct);
            }
            else if (delivery.Notification is not null && delivery.NotificationUserId.HasValue)
            {
                var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
                await sender.SendToUserAsync(
                    delivery.NotificationUserId.Value,
                    delivery.Notification,
                    ct);
            }
            else if (delivery.RealtimePayload is not null &&
                     delivery.RealtimeUserId.HasValue &&
                     !string.IsNullOrWhiteSpace(delivery.RealtimeEventName))
            {
                var sender = scope.ServiceProvider.GetRequiredService<IUserRealtimeEventSender>();
                await sender.SendAsync(
                    delivery.RealtimeUserId.Value,
                    delivery.RealtimeEventName,
                    delivery.RealtimePayload,
                    ct);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Delivery {delivery.DeliveryKey} has no dispatch payload.");
            }

            return DeliveryOutcome.Delivered(
                delivery.Lease,
                delivery.DeliveryKey,
                delivery.AttemptCount,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FailedOutcome(
                delivery.Lease,
                delivery.DeliveryKey,
                delivery.AttemptCount,
                ex);
        }
    }

    private async Task PersistOutcomesAsync(
        IReadOnlyCollection<DeliveryOutcome> outcomes,
        CancellationToken ct)
    {
        if (outcomes.Count == 0)
        {
            return;
        }

        await _databaseGate.RunAsync(async () =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var databaseToken = timeout.Token;
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            foreach (var outcome in outcomes)
            {
                int updated;
                if (outcome.Succeeded)
                {
                    updated = await context.Set<DeliveryOutbox>()
                        .Where(x =>
                            x.DeliveryOutboxId == outcome.Lease.DeliveryOutboxId &&
                            x.Status == (int)DeliveryOutboxStatus.Processing &&
                            x.ClaimToken == outcome.Lease.ClaimToken)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.Status, (int)DeliveryOutboxStatus.Delivered)
                            .SetProperty(x => x.DeliveredAt, outcome.DeliveredAt)
                            .SetProperty(x => x.ClaimToken, (Guid?)null)
                            .SetProperty(x => x.LastError, (string?)null), databaseToken);
                }
                else
                {
                    updated = await context.Set<DeliveryOutbox>()
                        .Where(x =>
                            x.DeliveryOutboxId == outcome.Lease.DeliveryOutboxId &&
                            x.Status == (int)DeliveryOutboxStatus.Processing &&
                            x.ClaimToken == outcome.Lease.ClaimToken)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.Status, outcome.DeadLettered
                                ? (int)DeliveryOutboxStatus.DeadLettered
                                : (int)DeliveryOutboxStatus.Pending)
                            .SetProperty(x => x.AttemptCount, outcome.AttemptCount)
                            .SetProperty(x => x.NextAttemptAt, outcome.NextAttemptAt!.Value)
                            .SetProperty(x => x.ClaimToken, (Guid?)null)
                            .SetProperty(x => x.LastError, outcome.Error), databaseToken);
                }

                LogOutcome(outcome, updated);
            }
        }, ct);
    }

    private void LogOutcome(DeliveryOutcome outcome, int updated)
    {
        var isEsignRevision = outcome.DeliveryKey.StartsWith("esign-revision:", StringComparison.Ordinal);
        if (updated == 0)
        {
            _logger.LogInformation(
                "Delivery {DeliveryKey} finished after lease ownership changed; the current state was preserved.",
                outcome.DeliveryKey);
            return;
        }

        if (outcome.Succeeded)
        {
            if (isEsignRevision)
            {
                ESignTelemetry.RecordRevisionEvent("delivered");
            }
            _logger.LogInformation(
                "Delivery {DeliveryKey} completed successfully.",
                outcome.DeliveryKey);
            return;
        }

        if (outcome.DeadLettered)
        {
            if (isEsignRevision)
            {
                ESignTelemetry.RecordRevisionEvent("dead_lettered");
            }
            _logger.LogError(outcome.Exception,
                "Delivery {DeliveryKey} dead-lettered after {Attempts} attempts.",
                outcome.DeliveryKey,
                outcome.AttemptCount);
        }
        else
        {
            if (isEsignRevision)
            {
                ESignTelemetry.RecordRevisionEvent("retried");
            }
            _logger.LogWarning(outcome.Exception,
                "Delivery {DeliveryKey} failed; attempt {Attempt} scheduled for {NextAttemptAt}.",
                outcome.DeliveryKey,
                outcome.AttemptCount,
                outcome.NextAttemptAt);
        }
    }

    private static DeliveryOutcome FailedOutcome(
        DeliveryOutboxLease lease,
        string deliveryKey,
        int currentAttemptCount,
        Exception exception)
    {
        var now = DateTime.UtcNow;
        var attempt = currentAttemptCount + 1;
        var error = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
        if (error.Length > 2_000)
        {
            error = error[..2_000];
        }

        var deadLettered = IsPermanentFailure(exception) || attempt > RetryDelays.Length;
        return DeliveryOutcome.Failed(
            lease,
            deliveryKey,
            attempt,
            deadLettered,
            deadLettered ? now : now.Add(RetryDelays[attempt - 1]),
            error,
            exception);
    }

    private async Task ReleaseLeasesAsync(
        IReadOnlyCollection<DeliveryOutboxLease> leases)
    {
        if (leases.Count == 0)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _databaseGate.RunAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                foreach (var lease in leases)
                {
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

                // ExecuteUpdateAsync bypasses the change tracker, so the SaveChanges interceptor
                // never sees this re-arm — publish explicitly, or a sleeping instance would only
                // pick these back up at its next deadline instead of immediately.
                await PublishDeliveryOutboxSignalAsync(scope, timeout.Token);
            }, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Delivery outbox leases could not be released.");
        }
    }

    private async Task PublishDeliveryOutboxSignalAsync(IServiceScope scope, CancellationToken ct)
    {
        if (!_workSignalOptions.Enabled)
        {
            return;
        }

        try
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IWorkSignalPublisher>();
            await publisher.PublishAsync(WorkSignalChannels.DeliveryOutbox, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish the delivery outbox work signal.");
        }
    }

    private static TimeSpan NextIdleInterval(TimeSpan current, TimeSpan maximum) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, maximum.Ticks));

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
        var recovered = await _databaseGate.RunAsync(async () =>
        {
            var recoveredCount = 0;
            foreach (var channel in DeliveryChannels)
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                recoveredCount += await context.Set<DeliveryOutbox>()
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

            return recoveredCount;
        }, ct);

        if (recovered > 0)
        {
            _logger.LogWarning("Recovered {Count} expired delivery leases.", recovered);
            using var scope = _scopeFactory.CreateScope();
            await PublishDeliveryOutboxSignalAsync(scope, ct);
        }
    }

    private async Task CompactDeliveredPayloadsAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-_options.DeliveredPayloadRetentionDays);
        var total = 0;
        for (var batch = 0; batch < 10; batch++)
        {
            var result = await _databaseGate.RunAsync(async () =>
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
                    return (Selected: 0, Updated: 0);
                }

                var updated = await context.Set<DeliveryOutbox>()
                    .Where(x =>
                        ids.Contains(x.DeliveryOutboxId) &&
                        x.Status == (int)DeliveryOutboxStatus.Delivered &&
                        x.DeliveredAt <= cutoff)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Payload, CompactedPayload)
                        .SetProperty(x => x.LastError, (string?)null), ct);
                return (Selected: ids.Count, Updated: updated);
            }, ct);
            if (result.Selected == 0)
            {
                break;
            }

            total += result.Updated;
            if (result.Selected < _options.RetentionBatchSize)
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
        return await _databaseGate.RunAsync(async () =>
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
        }, ct);
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

    private async Task<PreparedDelivery> PrepareFinalContractEmailAsync(
        IApplicationDbContext context,
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        if (job.Channel != (int)DeliveryChannel.Email)
        {
            throw new InvalidOperationException("ESign outbox deliveries only support email.");
        }

        var payload = JsonSerializer.Deserialize<ContractEsignDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid ESign contract delivery payload.");
        var documentMetadata = await context.Set<EsignDocument>()
            .AsNoTracking()
            .Where(item => item.EsignDocumentsId == payload.DocumentId)
            .Select(item => new
            {
                item.Status,
                item.DocumentCode,
                item.DocumentHash,
                item.PdfDocumentHash,
                item.PdfSignatureCount
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The finalized ESign document no longer exists.");
        var pdfArtifact = await ESignArtifactStorage.GetAsync(
            context,
            payload.DocumentId,
            ESignArtifactType.Pdf,
            "ESign.Artifact.Pdf.Email",
            ct);
        var document = new FinalContractArtifact(
            documentMetadata.Status,
            documentMetadata.DocumentCode,
            pdfArtifact?.Content,
            pdfArtifact?.FileName,
            pdfArtifact?.MimeType,
            documentMetadata.DocumentHash,
            documentMetadata.PdfDocumentHash,
            documentMetadata.PdfSignatureCount);
        if (document.Status != (int)ESignDocumentStatus.FullySigned ||
            document.Content is not { Length: > 0 } ||
            string.IsNullOrWhiteSpace(document.FileName) ||
            string.IsNullOrWhiteSpace(document.MimeType) ||
            document.PdfSignatureCount != 2 ||
            !string.Equals(
                document.PdfDocumentHash,
                $"{document.DocumentHash}{ESignPdfArtifactRevision.ContractTemplate}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The finalized ESign PDF artifact is not available.");
        }

        var signedEmail = _signedEmailRenderer.Render(new SignedEmailModel(
            payload.RecipientName,
            payload.ContractTitle,
            document.DocumentCode));
        return PreparedDelivery.ForEmail(
            lease,
            job.DeliveryKey,
            job.AttemptCount,
            new EmailRequest
            {
                To = payload.Email,
                Subject = signedEmail.Subject,
                Body = signedEmail.HtmlBody,
                TextBody = signedEmail.TextBody,
                IsHtml = true,
                IdempotencyKey = job.DeliveryKey,
                MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>",
                ByteAttachments =
                [
                    new EmailByteAttachment(
                        ESignPdfArtifactRevision.ContractFileName,
                        document.Content,
                        document.MimeType)
                ]
            });
    }

    private async Task<PreparedDelivery> PrepareMilestoneSubmissionEmailAsync(
        IApplicationDbContext context,
        DeliveryOutboxLease lease,
        DeliveryOutbox job,
        CancellationToken ct)
    {
        if (job.Channel != (int)DeliveryChannel.Email)
        {
            throw new InvalidOperationException("Milestone submission outbox deliveries only support email.");
        }

        var payload = JsonSerializer.Deserialize<MilestoneSubmissionDeliveryPayload>(job.Payload, JsonOptions)
            ?? throw new JsonException("Invalid milestone submission delivery payload.");

        var contract = await context.Set<Contract>()
            .AsNoTracking()
            .Include(c => c.JobPosts)
            .Include(c => c.Milestones)
            .Include(c => c.FreelancerProfiles).ThenInclude(f => f!.User)
            .FirstOrDefaultAsync(c => c.ContractsId == payload.ContractId, ct)
            ?? throw new InvalidOperationException("The contract for this milestone submission no longer exists.");

        var milestone = contract.Milestones.FirstOrDefault(m => m.MilestonesId == payload.MilestoneId)
            ?? throw new InvalidOperationException("The submitted milestone no longer exists.");

        var attachments = await context.Set<MilestoneAttachment>()
            .AsNoTracking()
            .Where(a => a.MilestonesId == milestone.MilestonesId)
            .ToListAsync(ct);

        var orderedMilestones = MilestoneWorkflowGuard.OrderMilestones(contract.Milestones.AsQueryable()).ToList();
        var milestoneNumber = orderedMilestones.FindIndex(m => m.MilestonesId == milestone.MilestonesId) + 1;
        var freelancerName = contract.FreelancerProfiles?.User?.FullName ?? "Freelancer";

        var files = attachments
            .Select(a => new MilestoneSubmissionFileModel(
                a.FileName,
                MilestoneAttachmentPresentation.TypeLabel(a.FileName, a.MimeType),
                MilestoneAttachmentPresentation.SizeLabel(a.FileSize),
                MilestoneAttachmentPresentation.IconGlyph(a.FileName)))
            .ToList();

        var model = new MilestoneSubmissionEmailModel(
            payload.ClientName,
            contract.JobPosts.Title,
            milestone.Title,
            milestoneNumber,
            orderedMilestones.Count,
            milestone.StartedAt,
            milestone.DueDate,
            milestone.SubmittedAt ?? DateTime.UtcNow,
            "Submitted",
            freelancerName,
            files,
            $"{_frontendBaseUrl}/contracts/{contract.ContractsId}/milestones/{milestone.MilestonesId}/approve");

        var rendered = _milestoneSubmissionEmailRenderer.Render(model);

        return PreparedDelivery.ForEmail(
            lease,
            job.DeliveryKey,
            job.AttemptCount,
            new EmailRequest
            {
                To = payload.ClientEmail,
                Subject = rendered.Subject,
                Body = rendered.HtmlBody,
                TextBody = rendered.TextBody,
                IsHtml = true,
                IdempotencyKey = job.DeliveryKey,
                MessageId = $"<{job.DeliveryKey.Replace(':', '.')}@gigbridge.local>"
            });
    }

    private static NotificationDto ToNotificationDto(Notification notification) => new()
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
    };

    private static NotificationDto ToNotificationDto(
        BroadcastNotification notification,
        BroadcastNotificationRecipient recipient) => new()
    {
        Id = notification.BroadcastNotificationId,
        Source = "Broadcast",
        BroadcastNotificationId = notification.BroadcastNotificationId,
        BroadcastRecipientId = recipient.BroadcastNotificationRecipientId,
        ReadTargetId = recipient.BroadcastNotificationRecipientId,
        Type = (NotificationType)notification.Type,
        Title = notification.Title,
        Content = notification.Content,
        ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType,
        IsRead = recipient.IsRead ?? false,
        ReadAt = recipient.ReadAt,
        CreatedAt = recipient.CreatedAt
    };

    private static bool IsPermanentFailure(Exception exception) =>
        exception is JsonException or InvalidOperationException;

    private static ContractEsignDeliveryPayload? TryReadContractDeliveryPayload(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<ContractEsignDeliveryPayload>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsFinalContractPdfReady(EsignDocument document) =>
        document.Status == (int)ESignDocumentStatus.FullySigned &&
        document.PdfDocumentSizeBytes is > 0 &&
        document.PdfSignatureCount == 2 &&
        string.Equals(
            document.PdfDocumentHash,
            ESignPdfArtifactRevision.ExpectedHash(document),
            StringComparison.Ordinal);

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
        string? MimeType,
        string? DocumentHash,
        string? PdfDocumentHash,
        int PdfSignatureCount);

    private sealed record PreparedDelivery(
        DeliveryOutboxLease Lease,
        string DeliveryKey,
        int AttemptCount,
        EmailRequest? Email,
        Guid? NotificationUserId,
        NotificationDto? Notification,
        Guid? RealtimeUserId,
        string? RealtimeEventName,
        object? RealtimePayload)
    {
        public static PreparedDelivery ForEmail(
            DeliveryOutboxLease lease,
            string deliveryKey,
            int attemptCount,
            EmailRequest email) =>
            new(lease, deliveryKey, attemptCount, email, null, null, null, null, null);

        public static PreparedDelivery ForNotification(
            DeliveryOutboxLease lease,
            string deliveryKey,
            int attemptCount,
            Guid userId,
            NotificationDto notification) =>
            new(lease, deliveryKey, attemptCount, null, userId, notification, null, null, null);

        public static PreparedDelivery ForRealtimeEvent(
            DeliveryOutboxLease lease,
            string deliveryKey,
            int attemptCount,
            Guid userId,
            string eventName,
            object payload) =>
            new(lease, deliveryKey, attemptCount, null, null, null, userId, eventName, payload);
    }

    private sealed record PreparedDeliveryBatch(
        IReadOnlyList<DeliveryOutboxLease> Leases,
        IReadOnlyList<PreparedDelivery> Deliveries,
        IReadOnlyList<DeliveryOutcome> PreparationOutcomes)
    {
        public static PreparedDeliveryBatch Empty { get; } = new([], [], []);
    }

    private sealed record DeliveryOutcome(
        DeliveryOutboxLease Lease,
        string DeliveryKey,
        int AttemptCount,
        bool Succeeded,
        bool DeadLettered,
        DateTime? DeliveredAt,
        DateTime? NextAttemptAt,
        string? Error,
        Exception? Exception)
    {
        public static DeliveryOutcome Delivered(
            DeliveryOutboxLease lease,
            string deliveryKey,
            int attemptCount,
            DateTime deliveredAt) =>
            new(lease, deliveryKey, attemptCount, true, false, deliveredAt, null, null, null);

        public static DeliveryOutcome Failed(
            DeliveryOutboxLease lease,
            string deliveryKey,
            int attemptCount,
            bool deadLettered,
            DateTime nextAttemptAt,
            string error,
            Exception exception) =>
            new(
                lease,
                deliveryKey,
                attemptCount,
                false,
                deadLettered,
                null,
                nextAttemptAt,
                error,
                exception);
    }
}
