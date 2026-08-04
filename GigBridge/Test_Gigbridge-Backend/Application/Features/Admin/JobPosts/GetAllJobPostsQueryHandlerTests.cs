using Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Features.Admin.JobPosts;

public class GetAllJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ClampsOversizedPagesAndReturnsAllStatusCounts()
    {
        await using var context = CreateContext();
        var client = AddClient(context);

        for (var index = 0; index < 12; index++)
        {
            AddJob(context, client, $"Job {index:00}", index % 4, index == 0 ? 3 : 0);
        }

        await context.SaveChangesAsync();

        var handler = new GetAllJobPostsQueryHandler(context);
        var result = await handler.Handle(
            new GetAllJobPostsQuery(PageIndex: 1, PageSize: 200),
            CancellationToken.None);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(12, result.Items.Count);
        Assert.Equal(12, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(new AdminJobPostStatsDto(12, 3, 3, 3, 3, 1), result.Stats);
    }

    [Fact]
    public async Task Handle_FiltersCancelledJobsAndKeepsGlobalCounts()
    {
        await using var context = CreateContext();
        var client = AddClient(context);
        AddJob(context, client, "Open product design", status: 1, visibility: 0);
        AddJob(context, client, "Cancelled product design", status: 3, visibility: 0);
        await context.SaveChangesAsync();

        var handler = new GetAllJobPostsQueryHandler(context);
        var result = await handler.Handle(
            new GetAllJobPostsQuery(PageSize: 25, Search: "product", Status: 3),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Cancelled product design", item.Title);
        Assert.Equal(1, result.TotalItems);
        var stats = Assert.IsType<AdminJobPostStatsDto>(result.Stats);
        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Open);
        Assert.Equal(1, stats.Cancelled);
    }

    [Fact]
    public async Task Handle_IgnoresStaleKnownTotalAndCountsFilteredQueryWithoutSummary()
    {
        await using var context = CreateContext();
        var client = AddClient(context);
        for (var index = 0; index < 23; index++)
        {
            AddJob(context, client, $"Paged Job {index:00}", status: 1, visibility: 0);
        }
        AddJob(context, client, "Cancelled Job", status: 3, visibility: 0);
        await context.SaveChangesAsync();

        var handler = new GetAllJobPostsQueryHandler(context);
        var result = await handler.Handle(
            new GetAllJobPostsQuery(
                PageIndex: 2,
                PageSize: 10,
                Status: 1,
                IncludeSummary: false,
                KnownTotalItems: 0),
            CancellationToken.None);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(23, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Null(result.Stats);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static ClientProfile AddClient(GigbridgeDbContext context)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin Jobs Test Client",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = 0,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        var client = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            CompanyName = "Test Company",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.ClientProfiles.Add(client);
        return client;
    }

    private static void AddJob(
        GigbridgeDbContext context,
        ClientProfile client,
        string title,
        int status,
        int visibility)
    {
        context.JobPosts.Add(new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = client.ClientProfilesId,
            ClientProfiles = client,
            Title = title,
            Description = $"Description for {title}",
            BudgetMin = 100,
            BudgetMax = 200,
            Status = status,
            Visibility = visibility,
            CustomSkillNames = ["Product"],
            CreatedAt = DateTime.UtcNow
        });
    }
}
