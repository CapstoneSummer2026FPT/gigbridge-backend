using Application.Features.Proposals.Freelancer.SubmitProposal.Commands;
using Application.Features.Proposals.Freelancer.SubmitProposal.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Proposals;

public class SubmitProposalValidatorTests
{
    private readonly SubmitProposalValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new SubmitProposalCommand(CreateValidRequest(), Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenJobPostIdIsEmpty()
    {
        var request = CreateValidRequest() with { JobPostsId = Guid.Empty };
        var command = new SubmitProposalCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.JobPostsId");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenCoverLetterIsTooShort()
    {
        var request = CreateValidRequest() with { CoverLetter = "Short cover letter." };
        var command = new SubmitProposalCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.CoverLetter");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenProposedRateIsNegative()
    {
        var request = CreateValidRequest() with { ProposedRate = -1m };
        var command = new SubmitProposalCommand(request, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.ProposedRate");
    }

    private static SubmitProposalRequest CreateValidRequest()
    {
        return new SubmitProposalRequest(
            JobPostsId: Guid.NewGuid(),
            CoverLetter: "I can deliver this feature with clean architecture and clear communication throughout the project.",
            ProposedRate: 500m,
            ProposedDuration: "10 days");
    }
}
