using Application.Features.Proposals.Client.GetProposalsByJobPost.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Client;

public class GetProposalsByJobPostQueryValidatorTests
{
    private readonly GetProposalsByJobPostQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetProposalsByJobPostQuery { JobPostsId = Guid.NewGuid(), UserId = Guid.NewGuid() });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenJobPostIdIsEmpty()
    {
        var result = _validator.Validate(new GetProposalsByJobPostQuery { JobPostsId = Guid.Empty, UserId = Guid.NewGuid() });

        Assert.Contains(result.Errors, error => error.PropertyName == "JobPostsId");
    }
}
