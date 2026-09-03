using Application.Common.Interfaces;
using Application.Common.InternalServices.JobPosts.BackgroundJobs;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.JobPosts.BackgroundJobs;

public sealed class JobPostExpirationServiceTests
{
    [Fact]
    public async Task ProcessOnceAsync_ClosesOpenJobWithPastEndDate_AndNotifiesClient()
    {
        var client = CreateClient();
        var job = CreateJob(client.ClientProfilesId, status: 1, endDate: DateTime.UtcNow.AddDays(-1));
        var context = new InMemoryApplicationDbContext();
        context.AddSet(client);
        context.AddSet(job);
        context.AddSet<Notification>();
        var worker = CreateWorker(context);

        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(2, job.Status);
        var notification = Assert.Single(context.Set<Notification>());
        Assert.Equal(client.UserId, notification.UserId);
        Assert.Equal(job.JobPostsId, notification.ReferenceId);
        Assert.Equal("JobPost", notification.ReferenceType);
    }

    [Fact]
    public async Task ProcessOnceAsync_LeavesOpenJobWithFutureEndDateUntouched()
    {
        var client = CreateClient();
        var job = CreateJob(client.ClientProfilesId, status: 1, endDate: DateTime.UtcNow.AddDays(1));
        var context = new InMemoryApplicationDbContext();
        context.AddSet(client);
        context.AddSet(job);
        context.AddSet<Notification>();
        var worker = CreateWorker(context);

        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, job.Status);
        Assert.Empty(context.Set<Notification>());
    }

    [Fact]
    public async Task ProcessOnceAsync_LeavesOpenJobWithNoEndDateUntouched()
    {
        var client = CreateClient();
        var job = CreateJob(client.ClientProfilesId, status: 1, endDate: null);
        var context = new InMemoryApplicationDbContext();
        context.AddSet(client);
        context.AddSet(job);
        context.AddSet<Notification>();
        var worker = CreateWorker(context);

        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, job.Status);
        Assert.Empty(context.Set<Notification>());
    }

    [Theory]
    [InlineData(0)] // Draft
    [InlineData(2)] // Closed
    [InlineData(3)] // Cancelled
    public async Task ProcessOnceAsync_LeavesNonOpenJobsWithPastEndDateUntouched(int status)
    {
        var client = CreateClient();
        var job = CreateJob(client.ClientProfilesId, status: status, endDate: DateTime.UtcNow.AddDays(-1));
        var context = new InMemoryApplicationDbContext();
        context.AddSet(client);
        context.AddSet(job);
        context.AddSet<Notification>();
        var worker = CreateWorker(context);

        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(status, job.Status);
        Assert.Empty(context.Set<Notification>());
    }

    [Fact]
    public async Task ProcessOnceAsync_ClosesAllExpiredCandidatesInASingleSaveChangesCall()
    {
        var client = CreateClient();
        var expiredOne = CreateJob(client.ClientProfilesId, status: 1, endDate: DateTime.UtcNow.AddDays(-2));
        var expiredTwo = CreateJob(client.ClientProfilesId, status: 1, endDate: DateTime.UtcNow.AddHours(-1));
        var context = new InMemoryApplicationDbContext();
        context.AddSet(client);
        context.AddSet(expiredOne, expiredTwo);
        context.AddSet<Notification>();
        var worker = CreateWorker(context);

        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(2, expiredOne.Status);
        Assert.Equal(2, expiredTwo.Status);
        Assert.Equal(2, context.Set<Notification>().Count());
        Assert.Equal(1, context.SaveChangesCount);
    }

    private static JobPostExpirationService CreateWorker(IApplicationDbContext context)
    {
        var services = new ServiceCollection()
            .AddSingleton(context)
            .BuildServiceProvider();
        return new JobPostExpirationService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobPostExpirationService>.Instance);
    }

    private static ClientProfile CreateClient()
    {
        return new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
    }

    private static JobPost CreateJob(Guid clientProfilesId, int status, DateTime? endDate)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfilesId,
            Title = "Sample job",
            Description = "Sample description",
            Status = status,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
    }
}
