using Application.Common.Exceptions;
using Application.Features.WorkExperiences.Common.DTOs;
using Application.Features.WorkExperiences.CreateWorkExperience.Commands;
using Application.Features.WorkExperiences.DeleteWorkExperience.Commands;
using Application.Features.WorkExperiences.GetWorkExperiences.Queries;
using Application.Features.WorkExperiences.UpdateWorkExperience.Commands;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Features.WorkExperiences;

public sealed class WorkExperienceHandlerTests
{
    [Fact]
    public async Task Handlers_CreateReadUpdateAndDeleteOwnedWorkExperience()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = AddFreelancer(context, userId);
        await context.SaveChangesAsync();

        var created = await new CreateWorkExperienceCommandHandler(context).Handle(
            new CreateWorkExperienceCommand(userId, new WorkExperienceInputDto
            {
                CompanyName = " GigBridge ",
                JobTitle = " Backend Engineer ",
                StartDate = new DateOnly(2024, 1, 1),
                EndDate = new DateOnly(2025, 1, 1),
                Description = " Built APIs "
            }),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.WorkExperienceId);
        Assert.Equal("GigBridge", created.CompanyName);
        Assert.Equal("Backend Engineer", created.JobTitle);
        Assert.Equal(profile.FreelancerProfilesId,
            (await context.WorkExperiences.SingleAsync()).FreelancerId);

        var listed = await new GetWorkExperiencesQueryHandler(context).Handle(
            new GetWorkExperiencesQuery(userId),
            CancellationToken.None);

        Assert.Equal(created.WorkExperienceId, Assert.Single(listed).WorkExperienceId);
        Assert.Equal("2024-01-01", listed[0].StartDate);
        Assert.Equal("2025-01-01", listed[0].EndDate);

        var updated = await new UpdateWorkExperienceCommandHandler(context).Handle(
            new UpdateWorkExperienceCommand(userId, created.WorkExperienceId, new WorkExperienceInputDto
            {
                CompanyName = "GigBridge Labs",
                JobTitle = "Senior Backend Engineer",
                StartDate = new DateOnly(2024, 1, 1),
                Description = " Leads API development "
            }),
            CancellationToken.None);

        Assert.Equal("GigBridge Labs", updated.CompanyName);
        Assert.Equal("Senior Backend Engineer", updated.JobTitle);
        Assert.Null(updated.EndDate);
        Assert.Equal("Leads API development", updated.Description);

        var deleted = await new DeleteWorkExperienceCommandHandler(context).Handle(
            new DeleteWorkExperienceCommand(userId, created.WorkExperienceId),
            CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(context.WorkExperiences);
    }

    [Fact]
    public async Task Update_RejectsWorkExperienceOwnedByAnotherFreelancer()
    {
        await using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownerProfile = AddFreelancer(context, ownerId);
        AddFreelancer(context, otherUserId);
        var experience = new WorkExperience
        {
            WorkExperiencesId = Guid.NewGuid(),
            FreelancerId = ownerProfile.FreelancerProfilesId,
            CompanyName = "Owner Company",
            Title = "Owner Role",
            StartDate = new DateOnly(2024, 1, 1)
        };
        context.WorkExperiences.Add(experience);
        await context.SaveChangesAsync();

        var action = () => new UpdateWorkExperienceCommandHandler(context).Handle(
            new UpdateWorkExperienceCommand(otherUserId, experience.WorkExperiencesId,
                new WorkExperienceInputDto
                {
                    CompanyName = "Other Company",
                    JobTitle = "Unauthorized Role",
                    StartDate = new DateOnly(2024, 1, 1)
                }),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal("Owner Company", experience.CompanyName);
        Assert.Equal("Owner Role", experience.Title);
    }

    [Fact]
    public void Validators_RejectMissingFieldsAndEndDateBeforeStartDate()
    {
        var dto = new WorkExperienceInputDto
        {
            CompanyName = "",
            JobTitle = "",
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2024, 12, 31)
        };

        var createResult = new CreateWorkExperienceCommandValidator().Validate(
            new CreateWorkExperienceCommand(Guid.NewGuid(), dto));
        var updateResult = new UpdateWorkExperienceCommandValidator().Validate(
            new UpdateWorkExperienceCommand(Guid.NewGuid(), Guid.NewGuid(), dto));

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("CompanyName"));
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("JobTitle"));
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("EndDate"));
    }

    private static GigbridgeDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FreelancerProfile AddFreelancer(GigbridgeDbContext context, Guid userId)
    {
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        context.FreelancerProfiles.Add(profile);
        return profile;
    }
}
