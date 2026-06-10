using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateStatusJobPostValidatorTests
{
    private readonly UpdateStatusJobPostCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Validate_ReturnsNoErrorsForSupportedStatus(int status)
    {
        var command = new UpdateStatusJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateStatusJobPostRequest { Status = status });

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenStatusIsUnsupported()
    {
        var command = new UpdateStatusJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateStatusJobPostRequest { Status = 4 });

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Status");
    }
}
