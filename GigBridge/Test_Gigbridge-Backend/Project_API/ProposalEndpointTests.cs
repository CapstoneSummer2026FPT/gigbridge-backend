using Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Project_API;

public class ProposalEndpointTests
{
    [Fact]
    public async Task SubmitProposal_ReturnsUnauthorized_WhenUserIsAnonymous()
    {
        using var factory = new GigBridgeApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/Proposals", CreateProposalBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitProposal_ReturnsBadRequest_WhenCoverLetterIsInvalid()
    {
        using var factory = new GigBridgeApiFactory();
        var (freelancerUserId, jobPostId, _) = await SeedProposalScenario(factory);
        var client = factory.CreateAuthenticatedClient(freelancerUserId, nameof(UserRole.Freelancer));

        var response = await client.PostAsJsonAsync("/api/Proposals", new
        {
            jobPostsId = jobPostId,
            coverLetter = "short",
            proposedRate = 500m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAndUpdateProposal_ReturnSuccess_WhenUserIsFreelancer()
    {
        using var factory = new GigBridgeApiFactory();
        var (freelancerUserId, jobPostId, _) = await SeedProposalScenario(factory);
        var client = factory.CreateAuthenticatedClient(freelancerUserId, nameof(UserRole.Freelancer));

        var submitResponse = await client.PostAsJsonAsync("/api/Proposals", CreateProposalBody(jobPostId));
        var proposalId = await ReadGuidFromApiResponse(submitResponse);
        var updateResponse = await client.PutAsJsonAsync($"/api/Proposals/{proposalId}", new
        {
            coverLetter = "Updated cover letter",
            proposedRate = 600m,
            proposedDuration = "12 days"
        });
        var myProposalsResponse = await client.GetAsync("/api/Proposals/my-proposals");
        var myProposalByJobResponse = await client.GetAsync($"/api/Proposals/job/{jobPostId}/my-proposal");

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, myProposalsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, myProposalByJobResponse.StatusCode);
    }

    [Fact]
    public async Task GetProposalsByJobPost_ReturnsForbidden_WhenClientDoesNotOwnJobPost()
    {
        using var factory = new GigBridgeApiFactory();
        var (_, jobPostId, otherClientUserId) = await SeedProposalScenario(factory);
        var client = factory.CreateAuthenticatedClient(otherClientUserId, nameof(UserRole.Client));

        var response = await client.GetAsync($"/api/Proposals/job/{jobPostId}/proposals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProposalDetail_ReturnsNotFound_WhenProposalDoesNotExist()
    {
        using var factory = new GigBridgeApiFactory();
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId, nameof(UserRole.Freelancer));

        var response = await client.GetAsync($"/api/Proposals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(Guid FreelancerUserId, Guid JobPostId, Guid OtherClientUserId)> SeedProposalScenario(GigBridgeApiFactory factory)
    {
        var clientUserId = Guid.NewGuid();
        var otherClientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfile = TestData.ClientProfile(clientUserId);
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);

        await factory.SeedAsync(context =>
        {
            context.Users.Add(TestData.User(clientUserId, UserRole.Client));
            context.Users.Add(TestData.User(otherClientUserId, UserRole.Client));
            context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
            context.ClientProfiles.Add(clientProfile);
            context.ClientProfiles.Add(TestData.ClientProfile(otherClientUserId));
            context.FreelancerProfiles.Add(TestData.FreelancerProfile(freelancerUserId));
            context.JobPosts.Add(jobPost);
            return Task.CompletedTask;
        });

        return (freelancerUserId, jobPost.JobPostsId, otherClientUserId);
    }

    private static object CreateProposalBody(Guid jobPostId)
    {
        return new
        {
            jobPostsId = jobPostId,
            coverLetter = "I can deliver this feature with clean architecture and clear communication throughout the project.",
            proposedRate = 500m,
            proposedDuration = "10 days"
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
