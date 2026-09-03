using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using Application.Common.InternalServices.JobPosts.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class SaveDraftJobPostCommandValidatorTests
{
    private readonly SaveDraftJobPostCommandValidator _validator = new(new ContentModerationService());

    [Fact]
    public void Validate_ReturnsNoErrorsForValidDraft()
    {
        var request = CreateValidRequest();
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenTotalSkillsExceedTen()
    {
        var request = CreateValidRequest() with
        {
            SkillIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            CustomSkillNames = new List<string> { "Skill1", "Skill2", "Skill3", "Skill4", "Skill5" } // Total = 11
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("up to 10 skills"));
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenTotalSkillsEqualsTen()
    {
        var request = CreateValidRequest() with
        {
            SkillIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            CustomSkillNames = new List<string> { "Skill1", "Skill2", "Skill3", "Skill4", "Skill5" } // Total = 10
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenDraftContentModerationBlocksJobPost()
    {
        var request = CreateValidRequest() with
        {
            Title = "Buôn ma tuy",
            Description = "Draft content"
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName == "JobPostContent" &&
                error.ErrorMessage == "Job post appears to request or promote illegal drug-related work.");
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenDraftTitleAndDescriptionAreEmpty()
    {
        var request = CreateValidRequest() with
        {
            Title = null,
            Description = null
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenMilestoneHasNoWorkItems()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto> { CreateMilestone("2 weeks", new List<JobPostWorkItemDto>()) }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenWorkItemDurationSumEqualsMilestoneDuration()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("2 weeks", new List<JobPostWorkItemDto>
                {
                    CreateWorkItem(0, "7 days"),
                    CreateWorkItem(1, "7 days"),
                })
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenWorkItemDurationSumIsLessThanMilestoneDuration()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("2 weeks", new List<JobPostWorkItemDto>
                {
                    CreateWorkItem(0, "3 days"),
                })
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenWorkItemDurationUsesDays()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("1 week", new List<JobPostWorkItemDto>
                {
                    CreateWorkItem(0, "3 days"),
                })
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenWorkItemDurationSumExceedsMilestoneDuration()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("1 week", new List<JobPostWorkItemDto>
                {
                    CreateWorkItem(0, "4 days"),
                    CreateWorkItem(1, "4 days"),
                })
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("exceed milestone duration by 1 day"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenWorkItemDurationIsUnparseable()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("2 weeks", new List<JobPostWorkItemDto>
                {
                    CreateWorkItem(0, "garbage"),
                })
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("day(s), week(s), month(s), or year(s)"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenMilestoneDurationItselfUsesDays()
    {
        var request = CreateValidRequest() with
        {
            MilestonePlans = new List<JobPostMilestonePlanDto>
            {
                CreateMilestone("3 days", new List<JobPostWorkItemDto>())
            }
        };
        var command = new SaveDraftJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "EstimatedDuration must be a number followed by week(s), month(s), or year(s).");
    }

    private static JobPostMilestonePlanDto CreateMilestone(string estimatedDuration, List<JobPostWorkItemDto> workItems) => new()
    {
        Title = "Milestone",
        Amount = 100m,
        EstimatedDuration = estimatedDuration,
        OrderIndex = 0,
        WorkItems = workItems,
    };

    private static JobPostWorkItemDto CreateWorkItem(int orderIndex, string estimatedDuration) => new()
    {
        Title = $"Work item {orderIndex}",
        EstimatedDuration = estimatedDuration,
        OrderIndex = orderIndex,
    };

    private static SaveDraftJobPostRequest CreateValidRequest()
    {
        return new SaveDraftJobPostRequest(
            Title: "Draft Title",
            Description: "Draft Description",
            MajorCategoryId: Guid.NewGuid(),
            BudgetMin: 500m,
            BudgetMax: 1000m,
            Currency: "USD",
            EstimatedDuration: "2-4 weeks",
            Visibility: 0,
            EndDate: DateTime.UtcNow.AddDays(7),
            IsAigenerated: false,
            SkillIds: new List<Guid> { Guid.NewGuid() },
            CustomSkillNames: new List<string> { "API" },
            Questions: null
        );
    }
}
