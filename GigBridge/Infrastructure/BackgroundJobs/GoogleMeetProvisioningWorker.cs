using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class GoogleMeetProvisioningWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaseMonitorInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoogleMeetProvisioningWorker> _logger;

    public GoogleMeetProvisioningWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<GoogleMeetProvisioningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Google Meet provisioning worker started");

        _ = RunLeaseMonitorAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning worker encountered an error");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Google Meet provisioning worker stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var meetApi = scope.ServiceProvider.GetRequiredService<IGoogleMeetApiClient>();
        var meetOAuth = scope.ServiceProvider.GetRequiredService<IGoogleMeetOAuthService>();
        var chat = scope.ServiceProvider.GetRequiredService<IChatRealtimeNotifier>();

        var now = DateTime.UtcNow;
        var dbContext = (DbContext)context;
        var jobIds = new List<Guid>();

        // Claim pending jobs atomically using the DbContext's connection
        var conn = dbContext.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
            UPDATE ""GoogleMeetProvisioningJobs"" j
            SET ""Status"" = @processing,
                ""StartedAt"" = @now,
                ""LeaseExpiresAt"" = @now + interval '60 seconds'
            WHERE j.""GoogleMeetProvisioningJobId"" IN (
                SELECT j2.""GoogleMeetProvisioningJobId""
                FROM ""GoogleMeetProvisioningJobs"" j2
                INNER JOIN ""Schedules"" s ON j2.""ScheduleId"" = s.""ScheduleId""
                INNER JOIN ""Conversations"" c ON s.""ConversationId"" = c.""ConversationsId""
                WHERE j2.""Status"" = @pending
                  AND j2.""Attempt"" = s.""MeetingAttempt""
                  AND s.""Status"" = @scheduled
                  AND s.""ScheduledAtUtc"" > @now
                  AND c.""Status"" = @active
                  AND s.""MeetingStatus"" = @meetPending
                ORDER BY j2.""CreatedAt""
                LIMIT 5
                FOR UPDATE SKIP LOCKED
            )
            RETURNING j.""GoogleMeetProvisioningJobId""";

        var nowParam = cmd.CreateParameter();
        nowParam.ParameterName = "now";
        nowParam.Value = now;
        cmd.Parameters.Add(nowParam);

        AddIntParam(cmd, "pending", (int)GoogleMeetProvisioningJobStatus.Pending);
        AddIntParam(cmd, "scheduled", (int)ScheduleStatus.Scheduled);
        AddIntParam(cmd, "active", (int)ConversationStatus.Active);
        AddIntParam(cmd, "meetPending", (int)MeetingProvisioningStatus.Pending);
        AddIntParam(cmd, "processing", (int)GoogleMeetProvisioningJobStatus.Processing);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                jobIds.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            await tx.CommitAsync(ct);
        }

        if (jobIds.Count == 0)
            return;

        var claimedJobs = await context.Set<GoogleMeetProvisioningJob>()
            .Where(j => jobIds.Contains(j.GoogleMeetProvisioningJobId))
            .ToListAsync(ct);

        foreach (var job in claimedJobs)
        {
            await ProcessJobAsync(job, dbContext, meetOAuth, meetApi, chat, ct);
        }
    }

    private async Task ProcessJobAsync(
        GoogleMeetProvisioningJob job,
        DbContext context,
        IGoogleMeetOAuthService meetOAuth,
        IGoogleMeetApiClient meetApi,
        IChatRealtimeNotifier chat,
        CancellationToken ct)
    {
        try
        {
            var accessToken = await meetOAuth.GetAccessTokenAsync(job.OrganizerUserId, ct);
            if (accessToken is null)
            {
                await CompleteJobAsync(context, job,
                    GoogleMeetProvisioningJobStatus.Failed, "token_refresh_failed",
                    null, null, chat, ct);
                return;
            }

            var result = await meetApi.CreateSpaceAsync(accessToken, ct);

            if (result.IsSuccess && result.SpaceName is not null && result.MeetingUri is not null)
            {
                await CompleteJobSuccessAsync(context, job,
                    result.SpaceName, result.MeetingUri, chat, ct);
            }
            else if (!result.IsAmbiguous)
            {
                await CompleteJobAsync(context, job,
                    GoogleMeetProvisioningJobStatus.Failed, result.FailureCode ?? "unknown",
                    null, null, chat, ct);
            }
            else
            {
                await CompleteJobAsync(context, job,
                    GoogleMeetProvisioningJobStatus.Ambiguous, result.FailureCode ?? "ambiguous",
                    null, null, chat, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process provisioning job {JobId}", job.GoogleMeetProvisioningJobId);
            await CompleteJobAsync(context, job,
                GoogleMeetProvisioningJobStatus.Ambiguous, "internal_error",
                null, null, chat, ct);
        }
    }

    private async Task CompleteJobAsync(
        DbContext context,
        GoogleMeetProvisioningJob job,
        GoogleMeetProvisioningJobStatus status,
        string? failureCode,
        string? spaceName,
        string? joinUri,
        IChatRealtimeNotifier chat,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        job.Status = status;
        job.FailureCode = failureCode;
        job.ReturnedSpaceName = spaceName;
        job.ReturnedJoinUri = joinUri;
        job.CompletedAt = now;
        job.AttemptCount++;

        var schedule = await context.Set<Schedule>()
            .Include(s => s.Conversation)
            .FirstOrDefaultAsync(s =>
                s.ScheduleId == job.ScheduleId &&
                s.Status == ScheduleStatus.Scheduled &&
                s.MeetingStatus == MeetingProvisioningStatus.Pending &&
                s.MeetingAttempt == job.Attempt, ct);

        if (schedule is not null)
        {
            schedule.MeetingLastAttemptAt = now;

            if (status == GoogleMeetProvisioningJobStatus.Succeeded)
            {
                schedule.MeetingStatus = MeetingProvisioningStatus.Ready;
                schedule.MeetingSpaceName = spaceName;
                schedule.MeetingJoinUri = joinUri;
                schedule.MeetingFailureCode = null;
            }
            else if (status == GoogleMeetProvisioningJobStatus.Failed)
            {
                schedule.MeetingStatus = MeetingProvisioningStatus.Failed;
                schedule.MeetingFailureCode = failureCode;
            }
            else
            {
                schedule.MeetingStatus = MeetingProvisioningStatus.Failed;
                schedule.MeetingFailureCode = failureCode ?? "ambiguous";
            }
        }

        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { }
        catch (DbUpdateException) { }

        await SendMeetingUpdateAsync(job.ScheduleId, context, chat, ct);
    }

    private async Task CompleteJobSuccessAsync(
        DbContext context,
        GoogleMeetProvisioningJob job,
        string spaceName,
        string joinUri,
        IChatRealtimeNotifier chat,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var currentJob = await context.Set<GoogleMeetProvisioningJob>()
            .FirstOrDefaultAsync(j =>
                j.GoogleMeetProvisioningJobId == job.GoogleMeetProvisioningJobId &&
                j.Status == GoogleMeetProvisioningJobStatus.Processing, ct);

        if (currentJob is null)
        {
            _logger.LogWarning("Job {JobId} is no longer Processing, skipping success", job.GoogleMeetProvisioningJobId);
            return;
        }

        currentJob.Status = GoogleMeetProvisioningJobStatus.Succeeded;
        currentJob.ReturnedSpaceName = spaceName;
        currentJob.ReturnedJoinUri = joinUri;
        currentJob.CompletedAt = now;
        currentJob.AttemptCount++;

        var schedule = await context.Set<Schedule>()
            .Include(s => s.Conversation)
            .FirstOrDefaultAsync(s =>
                s.ScheduleId == job.ScheduleId &&
                s.Status == ScheduleStatus.Scheduled &&
                s.MeetingStatus == MeetingProvisioningStatus.Pending &&
                s.MeetingAttempt == job.Attempt, ct);

        if (schedule is not null)
        {
            schedule.MeetingStatus = MeetingProvisioningStatus.Ready;
            schedule.MeetingSpaceName = spaceName;
            schedule.MeetingJoinUri = joinUri;
            schedule.MeetingFailureCode = null;
            schedule.MeetingLastAttemptAt = now;
        }

        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogInformation("Schedule {Id} was modified; job result saved but schedule not updated", job.ScheduleId);
        }
        catch (DbUpdateException)
        {
            _logger.LogInformation("Schedule {Id} was modified; job result saved but schedule not updated", job.ScheduleId);
        }

        await SendMeetingUpdateAsync(job.ScheduleId, context, chat, ct);
    }

    private async Task SendMeetingUpdateAsync(
        Guid scheduleId,
        DbContext context,
        IChatRealtimeNotifier chat,
        CancellationToken ct)
    {
        try
        {
            var schedule = await context.Set<Schedule>()
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);
            if (schedule is null) return;

            var participants = await context.Set<ConversationParticipant>()
                .Where(p => p.ConversationsId == schedule.ConversationId &&
                    p.LeftAt == null && p.DeletedAt == null)
                .ToListAsync(ct);

            foreach (var participant in participants.GroupBy(p => p.UserId).Select(p => p.First()))
            {
                var meeting = new
                {
                    provider = (int)schedule.MeetingProvider,
                    status = (int)schedule.MeetingStatus,
                    organizerUserId = schedule.CreatedByUserId,
                    joinUri = schedule.MeetingStatus == MeetingProvisioningStatus.Ready
                        ? schedule.MeetingJoinUri : null,
                    failureCode = schedule.MeetingFailureCode,
                    canRetry = participant.UserId == schedule.CreatedByUserId &&
                        schedule.MeetingStatus == MeetingProvisioningStatus.Failed &&
                        schedule.Status == ScheduleStatus.Scheduled &&
                        DateTime.UtcNow < schedule.ScheduledAtUtc
                };

                await chat.SendUserEventAsync(participant.UserId, "ScheduleMeetingChanged", new
                {
                    conversationId = schedule.ConversationId,
                    scheduleId = schedule.ScheduleId,
                    meeting
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send meeting update for schedule {ScheduleId}", scheduleId);
        }
    }

    private async Task RunLeaseMonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseMonitorInterval, ct);
                await ExpireStaleLeasesAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lease monitor encountered an error");
            }
        }
    }

    private async Task ExpireStaleLeasesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var staleCount = await context.Set<GoogleMeetProvisioningJob>()
            .Where(j => j.Status == GoogleMeetProvisioningJobStatus.Processing &&
                j.LeaseExpiresAt != null && j.LeaseExpiresAt < now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(j => j.Status, GoogleMeetProvisioningJobStatus.Ambiguous)
                    .SetProperty(j => j.FailureCode, "lease_expired")
                    .SetProperty(j => j.CompletedAt, now),
                ct);

        if (staleCount > 0)
        {
            _logger.LogWarning("Expired {Count} stale provisioning leases", staleCount);
        }
    }

    private static void AddIntParam(System.Data.Common.DbCommand cmd, string name, int value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
