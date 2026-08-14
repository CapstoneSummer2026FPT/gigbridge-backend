using Application.Features.Seo.PublicMarketplace.DTOs;
using Application.Features.Seo.PublicMarketplace.Queries;
using Application.Common.Exceptions;
using Domain.Entities;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Seo;

public sealed class PublicMarketplaceSeoTests
{
    [Fact]
    public void AnonymousContracts_DoNotExposePrivateContactOrAttachmentFields()
    {
        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Email",
            "UserEmail",
            "Phone",
            "PhoneNumber",
            "Attachments",
            "JobPostAttachments",
            "IdentityOrTaxCode"
        };

        Assert.Empty(typeof(PublicJobPostSummaryDto).GetProperties()
            .Where(property => forbiddenNames.Contains(property.Name)));
        Assert.Empty(typeof(PublicJobPostDetailDto).GetProperties()
            .Where(property => forbiddenNames.Contains(property.Name)));
        Assert.Empty(typeof(PublicFreelancerSummaryDto).GetProperties()
            .Where(property => forbiddenNames.Contains(property.Name)));
        Assert.Empty(typeof(PublicFreelancerProfileDto).GetProperties()
            .Where(property => forbiddenNames.Contains(property.Name)));
    }

    [Fact]
    public async Task SitemapResources_ContainsOnlyOpenPublicJobsAndOptedInActiveFreelancers()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        var publicJob = CreateJob(status: 1, visibility: 0, now.AddMinutes(-4));
        var legacyPublicJob = CreateJob(status: 1, visibility: null, now.AddMinutes(-3));
        var privateJob = CreateJob(status: 1, visibility: 1, now.AddMinutes(-2));
        var closedJob = CreateJob(status: 2, visibility: 0, now.AddMinutes(-1));
        context.JobPosts.AddRange(publicJob, legacyPublicJob, privateJob, closedJob);

        var optedInActive = CreateFreelancer(allowIndexing: true, isActive: true, now);
        var optedOutActive = CreateFreelancer(allowIndexing: false, isActive: true, now);
        var optedInInactive = CreateFreelancer(allowIndexing: true, isActive: false, now);
        context.Users.AddRange(optedInActive.User, optedOutActive.User, optedInInactive.User);
        context.FreelancerProfiles.AddRange(optedInActive, optedOutActive, optedInInactive);
        await context.SaveChangesAsync();

        var handler = new GetSeoSitemapResourcesQueryHandler(context);
        var result = await handler.Handle(new GetSeoSitemapResourcesQuery(), CancellationToken.None);

        Assert.Equal(
            new HashSet<Guid> { publicJob.JobPostsId, legacyPublicJob.JobPostsId },
            result.Jobs.Select(entry => entry.Id).ToHashSet());
        var freelancer = Assert.Single(result.Freelancers);
        Assert.Equal(optedInActive.UserId, freelancer.Id);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PublicFreelancerDetail_RejectsOptedOutOrInactiveProfiles(
        bool allowIndexing,
        bool isActive)
    {
        await using var context = CreateContext();
        var profile = CreateFreelancer(allowIndexing, isActive, DateTime.UtcNow);
        context.Users.Add(profile.User);
        context.FreelancerProfiles.Add(profile);
        await context.SaveChangesAsync();
        var mediator = Substitute.For<IMediator>();
        var handler = new GetPublicFreelancerProfileQueryHandler(context, mediator);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetPublicFreelancerProfileQuery(profile.UserId),
            CancellationToken.None));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static JobPost CreateJob(int status, int? visibility, DateTime updatedAt)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            Title = $"SEO job {Guid.NewGuid():N}",
            Description = "Public description",
            Status = status,
            Visibility = visibility,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt
        };
    }

    private static FreelancerProfile CreateFreelancer(
        bool allowIndexing,
        bool isActive,
        DateTime updatedAt)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "SEO Freelancer",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = 1,
            IsActive = isActive,
            CreatedAt = updatedAt.AddDays(-1)
        };
        return new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            AllowSearchEngineIndexing = allowIndexing,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt
        };
    }
}
