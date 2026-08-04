using Application.Features.Profiles.Common.UpdateUserProfile.Commands;
using Application.Features.Profiles.Common.UpdateUserProfile.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Common;

public sealed class UpdateUserProfileCommandValidatorTests
{
    private readonly UpdateUserProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidProfile()
    {
        var result = _validator.Validate(new UpdateUserProfileCommand(new UpdateUserProfileDto
        {
            FullName = "Valid User",
            Email = "valid@example.com",
            Avatar = "https://cdn.example.com/avatar.png",
            PhoneNumber = "+84901234567",
            PreferredLanguage = "vi"
        }));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsInvalidRequiredFieldsAndDatabaseLengths()
    {
        var result = _validator.Validate(new UpdateUserProfileCommand(new UpdateUserProfileDto
        {
            FullName = "",
            Email = "not-an-email",
            PhoneNumber = new string('1', 21),
            PreferredLanguage = "vi-VN-extra"
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.FullName");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.PhoneNumber");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.PreferredLanguage");
    }
}
