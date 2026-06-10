using Application.Features.Proposals.Freelancer.GetMyProposalByJobPost.Queries;
using Application.Features.Proposals.Freelancer.GetProposalDetail.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class GetMyProposalByJobPostQueryValidatorTests
{
    private readonly GetMyProposalByJobPostQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetMyProposalByJobPostQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenJobPostIdIsEmpty()
    {
        var result = _validator.Validate(new GetMyProposalByJobPostQuery(Guid.Empty, Guid.NewGuid()));

        Assert.Contains(result.Errors, error => error.PropertyName == "JobPostId");
    }
}
