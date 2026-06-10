using Application.Features.Proposals.Freelancer.GetMyProposals.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class GetMyProposalsQueryValidatorTests
{
    private readonly GetMyProposalsQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetMyProposalsQuery { UserId = Guid.NewGuid() });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenUserIdIsEmpty()
    {
        var result = _validator.Validate(new GetMyProposalsQuery { UserId = Guid.Empty });

        Assert.Contains(result.Errors, error => error.PropertyName == "UserId");
    }
}
