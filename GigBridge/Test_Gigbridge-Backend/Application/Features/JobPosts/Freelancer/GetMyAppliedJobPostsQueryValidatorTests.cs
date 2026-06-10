using Application.Features.JobPosts.Freelancer.GetMyAppliedJobPosts.Queries;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Freelancer;

public class GetMyAppliedJobPostsQueryValidatorTests
{
    private readonly GetMyAppliedJobPostsQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetMyAppliedJobPostsQuery { UserId = Guid.NewGuid() });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenPageIndexIsZero()
    {
        var result = _validator.Validate(new GetMyAppliedJobPostsQuery { UserId = Guid.NewGuid(), PageIndex = 0 });

        Assert.Contains(result.Errors, error => error.PropertyName == "PageIndex");
    }
}
