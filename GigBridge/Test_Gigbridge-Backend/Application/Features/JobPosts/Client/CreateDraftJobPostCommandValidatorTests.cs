using Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class CreateDraftJobPostCommandValidatorTests
{
    private readonly CreateDraftJobPostCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidUserId()
    {
        var result = _validator.Validate(new CreateDraftJobPostCommand(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenUserIdIsEmpty()
    {
        var result = _validator.Validate(new CreateDraftJobPostCommand(Guid.Empty));

        Assert.Contains(result.Errors, error => error.PropertyName == "UserId");
    }
}
