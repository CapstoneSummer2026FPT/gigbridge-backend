using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class UpdateProposalStatusValidatorTests
{
    private readonly UpdateProposalStatusCommandValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Validate_ReturnsNoErrorsForSupportedStatus(int status)
    {
        var command = new UpdateProposalStatusCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateProposalStatusRequest { Status = status });

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenStatusIsUnsupported()
    {
        var command = new UpdateProposalStatusCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateProposalStatusRequest { Status = 0 });

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Status");
    }
}
