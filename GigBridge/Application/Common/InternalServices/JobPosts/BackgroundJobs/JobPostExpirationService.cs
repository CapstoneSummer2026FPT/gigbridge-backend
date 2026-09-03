using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Common.InternalServices.JobPosts.BackgroundJobs;
public sealed class JobPostExpirationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobPostExpirationService> _logger;

    public JobPostExpirationService(
        IServiceScopeFactory scopeFactory,
        ILogger<JobPostExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job post expiration service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Error occurred while closing expired job posts.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;

        var expiredJobs = await context.Set<JobPost>()
            .Where(j => j.Status == 1 && j.EndDate != null && j.EndDate <= now)
            .OrderBy(j => j.EndDate)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (expiredJobs.Count == 0)
        {
            return;
        }

        var clientProfileIds = expiredJobs.Select(j => j.ClientProfilesId).Distinct().ToList();
        var clientProfiles = await context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(cp => clientProfileIds.Contains(cp.ClientProfilesId))
            .Select(cp => new { cp.ClientProfilesId, cp.UserId })
            .ToListAsync(cancellationToken);
        var profileToUser = clientProfiles.ToDictionary(cp => cp.ClientProfilesId, cp => cp.UserId);

        foreach (var job in expiredJobs)
        {
            job.Status = 2; // Closed
            job.UpdatedAt = now;

            if (!profileToUser.TryGetValue(job.ClientProfilesId, out var clientUserId))
            {
                continue;
            }

            context.Set<Notification>().Add(new Notification
            {
                UserId = clientUserId,
                Type = (int)NotificationType.SystemAlert,
                Title = "[Job Closed] Job posting deadline has passed",
                Content = $"Your job \"{job.Title}\" has automatically closed because its deadline ({job.EndDate:g} UTC) has passed.",
                ReferenceId = job.JobPostsId,
                ReferenceType = "JobPost",
                IsRead = false,
                CreatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Closed {Count} expired job post(s).", expiredJobs.Count);
    }
}
