using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices;

public class DeadlineWarningService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadlineWarningService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    public DeadlineWarningService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadlineWarningService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deadline warning service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred while checking deadlines.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var warningThreshold = now.AddHours(24);

        var approachingDeadlineJobs = await context.Set<JobPost>()
            .AsNoTracking()
            .Where(j => j.ApplicationDeadline != null
                && j.ApplicationDeadline > now
                && j.ApplicationDeadline <= warningThreshold
                && j.Status == 1) // 1 = Open/Active
            .Select(j => new
            {
                j.JobPostsId,
                j.Title,
                Deadline = j.ApplicationDeadline,
                j.ClientProfilesId
            })
            .ToListAsync(cancellationToken);

        if (approachingDeadlineJobs.Count == 0)
        {
            return;
        }

        // Resolve client user IDs from client profile IDs
        var clientProfileIds = approachingDeadlineJobs
            .Select(j => j.ClientProfilesId)
            .Distinct()
            .ToList();

        var clientUsers = await context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(cp => clientProfileIds.Contains(cp.ClientProfilesId))
            .Select(cp => new { cp.ClientProfilesId, cp.UserId })
            .ToListAsync(cancellationToken);

        var profileToUser = clientUsers.ToDictionary(cu => cu.ClientProfilesId, cu => cu.UserId);

        var notificationType = (int)NotificationType.SystemAlert;

        foreach (var job in approachingDeadlineJobs)
        {
            if (!profileToUser.TryGetValue(job.ClientProfilesId, out var clientUserId))
            {
                continue;
            }

            // Duplicate guard: check if a warning already exists for this job+user
            var alreadyWarned = await context.Set<Notification>()
                .AsNoTracking()
                .AnyAsync(n =>
                    n.UserId == clientUserId
                    && n.ReferenceId == job.JobPostsId
                    && n.ReferenceType == "JobPost"
                    && n.Type == notificationType
                    && n.Title.StartsWith("[Deadline Warning]"),
                    cancellationToken);

            if (alreadyWarned)
            {
                continue;
            }

            var remainingHours = (int)(job.Deadline!.Value - now).TotalHours;
            var notification = new Notification
            {
                UserId = clientUserId,
                Type = notificationType,
                Title = $"[Deadline Warning] Job deadline approaching",
                Content = $"Your job \"{job.Title}\" deadline is in approximately {remainingHours} hours ({job.Deadline:g} UTC).",
                ReferenceId = job.JobPostsId,
                ReferenceType = "JobPost",
                IsRead = false,
                CreatedAt = now
            };

            context.Set<Notification>().Add(notification);
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created {Count} deadline warning notifications.", approachingDeadlineJobs.Count);
    }
}
