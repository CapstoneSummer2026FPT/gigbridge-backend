using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateJobPostValidatorTests
{
    private readonly UpdateJobPostCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), CreateValidRequest());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenBudgetTypeIsUnsupported()
    {
        var request = CreateValidRequest();
        request.BudgetType = 3;
        var command = new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.BudgetType");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenMaxHiresIsZero()
    {
        var request = CreateValidRequest();
        request.MaxHires = 0;
        var command = new UpdateJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.MaxHires");
    }

    private static UpdateJobPostRequest CreateValidRequest()
    {
        return new UpdateJobPostRequest
        {
            Title = "Build a booking module",
            Description = "Create booking workflow and notification logic.",
            BudgetType = 0,
            BudgetMin = 500m,
            BudgetMax = 1000m,
            MaxHires = 1,
            ExperienceLevelRequired = 1,
            LocationType = 0,
            ApplicationDeadline = DateTime.UtcNow.AddDays(7)
        };
    }
}
