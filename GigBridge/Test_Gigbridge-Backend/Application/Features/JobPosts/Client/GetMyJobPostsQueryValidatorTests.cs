using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class GetMyJobPostsQueryValidatorTests
{
    private readonly GetMyJobPostsQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetMyJobPostsQuery { UserId = Guid.NewGuid() });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenUserIdIsEmpty()
    {
        var result = _validator.Validate(new GetMyJobPostsQuery { UserId = Guid.Empty });

        Assert.Contains(result.Errors, error => error.PropertyName == "UserId");
    }
}
