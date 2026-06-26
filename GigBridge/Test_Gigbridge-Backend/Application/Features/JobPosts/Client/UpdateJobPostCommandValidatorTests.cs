using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Infrastructure.Services;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateJobPostCommandValidatorTests
{
    private readonly UpdateJobPostCommandValidator _validator = new(new ContentModerationService());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_ReturnsNoErrorsForValidVisibility(int visibility)
    {
        var request = CreateValidRequest() with { Visibility = visibility };

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenVisibilityIsMissing()
    {
        var request = CreateValidRequest() with { Visibility = null };

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Visibility");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Validate_ReturnsErrorWhenVisibilityIsOutOfRange(int visibility)
    {
        var request = CreateValidRequest() with { Visibility = visibility };

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Visibility");
    }

    [Fact]
    public void Validate_StillReturnsErrorWhenBudgetMinIsGreaterThanBudgetMax()
    {
        var request = CreateValidRequest() with { BudgetMin = 200m, BudgetMax = 100m };

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request");
    }

    [Fact]
    public void Validate_ReturnsError_WhenTotalSkillsExceedTen()
    {
        var request = CreateValidRequest() with
        {
            SkillIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            CustomSkillNames = new List<string> { "Skill1", "Skill2", "Skill3", "Skill4", "Skill5" } // Total = 11
        };
        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

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
        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenContentModerationBlocksJobPost()
    {
        var request = CreateValidRequest() with
        {
            Title = "Security task",
            Description = "Viet malware va ddos website doi thu."
        };

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("community and legal safety standards"));
    }

    private static UpdateJobPostRequest CreateValidRequest()
    {
        return new UpdateJobPostRequest(
            Title: "Build a booking module",
            Description: "Create booking workflow and notification logic.",
            MajorCategoryId: Guid.NewGuid(),
            BudgetMin: 500m,
            BudgetMax: 1000m,
            Currency: "VND",
            EstimatedDuration: "2 weeks",
            Location: "Remote",
            Visibility: 1,
            EndDate: DateTime.UtcNow.AddDays(7),
            SkillIds: new List<Guid> { Guid.NewGuid() },
            CustomSkillNames: new List<string> { "API" });
    }
}
