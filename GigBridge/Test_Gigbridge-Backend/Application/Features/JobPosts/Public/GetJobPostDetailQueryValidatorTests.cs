using Application.Features.JobPosts.Public.GetJobPostDetail.Queries;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Public;

public class GetJobPostDetailQueryValidatorTests
{
    private readonly GetJobPostDetailQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetJobPostDetailQuery(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenJobPostIdIsEmpty()
    {
        var result = _validator.Validate(new GetJobPostDetailQuery(Guid.Empty));

        Assert.Contains(result.Errors, error => error.PropertyName == "JobPostsId");
    }
}
