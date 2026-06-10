using Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Project_API;

public class JobPostEndpointTests
{
    [Fact]
    public async Task GetPublicJobPosts_ReturnsSuccess_WhenRequestIsAnonymous()
    {
        using var factory = new GigBridgeApiFactory();
        await factory.SeedAsync(context =>
        {
            context.JobPosts.Add(TestData.JobPost(Guid.NewGuid(), title: "Public job", status: 1, visibility: 0));
            context.JobPosts.Add(TestData.JobPost(Guid.NewGuid(), title: "Closed job", status: 2, visibility: 0));
            return Task.CompletedTask;
        });

        var response = await factory.CreateClient().GetAsync("/api/JobPosts/public");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Public job", body);
        Assert.DoesNotContain("Closed job", body);
    }

    [Fact]
    public async Task GetJobPostDetail_ReturnsNotFound_WhenJobPostIsPrivate()
    {
        using var factory = new GigBridgeApiFactory();
        var jobPost = TestData.JobPost(Guid.NewGuid(), visibility: 1);
        await factory.SeedAsync(context =>
        {
            context.JobPosts.Add(jobPost);
            return Task.CompletedTask;
        });

        var response = await factory.CreateClient().GetAsync($"/api/JobPosts/{jobPost.JobPostsId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateJobPost_ReturnsUnauthorized_WhenUserIsAnonymous()
    {
        using var factory = new GigBridgeApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/JobPosts", CreateJobPostBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateJobPost_ReturnsBadRequest_WhenRequestIsInvalid()
    {
        using var factory = new GigBridgeApiFactory();
        var userId = Guid.NewGuid();
        await SeedClient(factory, userId);
        var client = factory.CreateAuthenticatedClient(userId, nameof(UserRole.Client));

        var response = await client.PostAsJsonAsync("/api/JobPosts", new
        {
            title = "",
            description = "",
            budgetType = 9,
            skillIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndUpdateJobPost_ReturnSuccess_WhenUserIsClient()
    {
        using var factory = new GigBridgeApiFactory();
        var userId = Guid.NewGuid();
        await SeedClient(factory, userId);
        var client = factory.CreateAuthenticatedClient(userId, nameof(UserRole.Client));

        var createResponse = await client.PostAsJsonAsync("/api/JobPosts", CreateJobPostBody());
        var jobPostId = await ReadGuidFromApiResponse(createResponse);
        var updateResponse = await client.PutAsJsonAsync($"/api/JobPosts/{jobPostId}", UpdateJobPostBody());
        var statusResponse = await client.PatchAsJsonAsync($"/api/JobPosts/{jobPostId}/status", new { status = 2 });
        var visibilityResponse = await client.PatchAsJsonAsync($"/api/JobPosts/{jobPostId}/visibility", new { visibility = 1 });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visibilityResponse.StatusCode);
    }

    [Fact]
    public async Task GetMyApplications_ReturnsForbidden_WhenUserIsClient()
    {
        using var factory = new GigBridgeApiFactory();
        var userId = Guid.NewGuid();

        var response = await factory.CreateAuthenticatedClient(userId, nameof(UserRole.Client))
            .GetAsync("/api/JobPosts/my-applications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedClient(GigBridgeApiFactory factory, Guid userId)
    {
        await factory.SeedAsync(context =>
        {
            context.Users.Add(TestData.User(userId, UserRole.Client));
            context.ClientProfiles.Add(TestData.ClientProfile(userId));
            return Task.CompletedTask;
        });
    }

    private static object CreateJobPostBody()
    {
        return new
        {
            title = "Build a booking module",
            description = "Create booking workflow and notification logic.",
            categoryId = Guid.NewGuid(),
            budgetType = 0,
            budgetMin = 500m,
            budgetMax = 1000m,
            currency = "VND",
            estimatedDuration = "2 weeks",
            maxHires = 1,
            experienceLevelRequired = 1,
            locationType = 0,
            location = "Remote",
            visibility = 1,
            applicationDeadline = DateTime.UtcNow.AddDays(7),
            skillIds = Array.Empty<Guid>()
        };
    }

    private static object UpdateJobPostBody()
    {
        return new
        {
            title = "Updated booking module",
            description = "Updated booking workflow.",
            budgetType = 0,
            budgetMin = 500m,
            budgetMax = 1000m,
            maxHires = 1,
            experienceLevelRequired = 1,
            locationType = 0,
            applicationDeadline = DateTime.UtcNow.AddDays(7),
            skillIds = Array.Empty<Guid>()
        };
    }

    private static async Task<Guid> ReadGuidFromApiResponse(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<ApiGuidResponse>();
        return json!.Data;
    }

    private sealed class ApiGuidResponse
    {
        public Guid Data { get; set; }
    }
}
