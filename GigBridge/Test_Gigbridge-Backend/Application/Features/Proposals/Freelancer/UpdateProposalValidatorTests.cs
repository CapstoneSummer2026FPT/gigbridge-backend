using Application.Features.Proposals.Freelancer.UpdateProposal.Commands;
using Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class UpdateProposalValidatorTests
{
    private readonly UpdateProposalCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new UpdateProposalCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateProposalRequest { ProposedRate = 500m });

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenProposedRateIsZero()
    {
        var command = new UpdateProposalCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateProposalRequest { ProposedRate = 0m });

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.ProposedRate");
    }
}
