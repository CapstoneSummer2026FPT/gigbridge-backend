using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Public;

public class GetAvailableJobPostsQueryValidatorTests
{
    private readonly GetAvailableJobPostsQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var query = new GetAvailableJobPostsQuery();

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenPageSizeIsTooLarge()
    {
        var query = new GetAvailableJobPostsQuery(PageSize: 101);

        var result = _validator.Validate(query);

        Assert.Contains(result.Errors, error => error.PropertyName == "PageSize");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenBudgetRangeIsInvalid()
    {
        var query = new GetAvailableJobPostsQuery(BudgetMin: 1000m, BudgetMax: 500m);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }
}
