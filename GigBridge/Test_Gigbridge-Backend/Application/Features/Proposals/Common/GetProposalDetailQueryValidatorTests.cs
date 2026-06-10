using Application.Features.Proposals.Common.GetProposalDetail.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class GetProposalDetailQueryValidatorTests
{
    private readonly GetProposalDetailQueryValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var result = _validator.Validate(new GetProposalDetailQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenProposalIdIsEmpty()
    {
        var result = _validator.Validate(new GetProposalDetailQuery(Guid.Empty, Guid.NewGuid()));

        Assert.Contains(result.Errors, error => error.PropertyName == "ProposalId");
    }
}
