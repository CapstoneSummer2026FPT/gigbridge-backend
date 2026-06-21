using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class SaveDraftJobPostCommandValidatorTests
{
    private readonly SaveDraftJobPostCommandValidator _validator = new();

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
            MaxHires: 1,
            Location: "Remote",
            Visibility: 1,
            EndDate: DateTime.UtcNow.AddDays(7),
            IsAigenerated: false,
            SkillIds: new List<Guid> { Guid.NewGuid() },
            CustomSkillNames: new List<string> { "API" },
            Questions: null
        );
    }
}
