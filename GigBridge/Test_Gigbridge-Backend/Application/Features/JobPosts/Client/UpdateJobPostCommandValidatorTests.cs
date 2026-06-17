using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateJobPostCommandValidatorTests
{
    private readonly UpdateJobPostCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_ReturnsNoErrorsForValidVisibility(int visibility)
    {
        var request = CreateValidRequest();
        request.Visibility = visibility;

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenVisibilityIsMissing()
    {
        var request = CreateValidRequest();
        request.Visibility = null;

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Visibility");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Validate_ReturnsErrorWhenVisibilityIsOutOfRange(int visibility)
    {
        var request = CreateValidRequest();
        request.Visibility = visibility;

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Visibility");
    }

    [Fact]
    public void Validate_StillReturnsErrorWhenBudgetMinIsGreaterThanBudgetMax()
    {
        var request = CreateValidRequest();
        request.BudgetMin = 200m;
        request.BudgetMax = 100m;

        var result = _validator.Validate(new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains(result.Errors, error => error.PropertyName == "Request");
    }

    private static UpdateJobPostRequest CreateValidRequest()
    {
        return new UpdateJobPostRequest
        {
            Title = "Build a booking module",
            Description = "Create booking workflow and notification logic.",
            CategoryId = Guid.NewGuid(),
            BudgetMin = 500m,
            BudgetMax = 1000m,
            Currency = "VND",
            EstimatedDuration = "2 weeks",
            MaxHires = 1,
            Location = "Remote",
            Visibility = 1,
            EndDate = DateTime.UtcNow.AddDays(7),
            SkillIds = new List<Guid> { Guid.NewGuid() }
        };
    }
}
