using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateVisibilityJobPostValidatorTests
{
    private readonly UpdateVisibilityJobPostCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_ReturnsNoErrorsForSupportedVisibility(int visibility)
    {
        var command = new UpdateVisibilityJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateVisibilityJobPostRequest { Visibility = visibility });

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenVisibilityIsUnsupported()
    {
        var command = new UpdateVisibilityJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateVisibilityJobPostRequest { Visibility = 3 });

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Visibility");
    }
}
