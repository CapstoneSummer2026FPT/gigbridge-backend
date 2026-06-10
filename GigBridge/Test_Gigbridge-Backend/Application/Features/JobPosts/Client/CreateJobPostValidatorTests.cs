using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class CreateJobPostValidatorTests
{
    private readonly CreateJobPostValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new CreateJobPostCommand(CreateValidRequest(), Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsNoErrorsWhenBudgetMinEqualsBudgetMax()
    {
        var request = CreateValidRequest() with { BudgetMin = 1000m, BudgetMax = 1000m };
        var command = new CreateJobPostCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenBudgetMinIsGreaterThanBudgetMax()
    {
        var request = CreateValidRequest() with { BudgetMin = 1001m, BudgetMax = 1000m };
        var command = new CreateJobPostCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.BudgetMax");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenBudgetMinIsNegative()
    {
        var request = CreateValidRequest() with { BudgetMin = -1m };
        var command = new CreateJobPostCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.BudgetMin");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenBudgetMaxIsNegative()
    {
        var request = CreateValidRequest() with { BudgetMax = -1m };
        var command = new CreateJobPostCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.BudgetMax");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenDeadlineIsInPast()
    {
        var request = CreateValidRequest() with { EndDate = DateTime.UtcNow.AddDays(-1) };
        var command = new CreateJobPostCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.EndDate");
    }

    private static CreateJobPostRequest CreateValidRequest()
    {
        return new CreateJobPostRequest(
            Title: "Build a booking module",
            Description: "Create booking workflow and notification logic.",
            CategoryId: Guid.NewGuid(),
            BudgetMin: 500m,
            BudgetMax: 1000m,
            Currency: "VND",
            EstimatedDuration: "2 weeks",
            MaxHires: 1,
            Location: "Remote",
            Visibility: 1,
            EndDate: DateTime.UtcNow.AddDays(7),
            SkillIds: new List<Guid> { Guid.NewGuid() });
    }
}
