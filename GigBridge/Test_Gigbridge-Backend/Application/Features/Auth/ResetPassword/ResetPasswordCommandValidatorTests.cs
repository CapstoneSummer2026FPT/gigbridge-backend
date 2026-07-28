using Application.Features.Auth.ResetPassword.Commands;
using Application.Features.Auth.ResetPassword.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidRequest()
    {
        var result = _validator.Validate(CreateValidCommand());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("ABC123")]
    public void Validate_RejectsInvalidOtp(string otp)
    {
        var command = CreateValidCommand();
        command.Request.Otp = otp;

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Otp");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password1")]
    [InlineData("password1!")]
    [InlineData("PASSWORD1!")]
    public void Validate_RejectsWeakPassword(string password)
    {
        var command = CreateValidCommand();
        command.Request.NewPassword = password;

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.NewPassword");
    }

    [Fact]
    public void Validate_RejectsInvalidEmail()
    {
        var command = CreateValidCommand();
        command.Request.Email = "not-an-email";

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Email");
    }

    private static ResetPasswordCommand CreateValidCommand() =>
        new(new ResetPasswordRequest
        {
            Email = "user@example.com",
            Otp = "123456",
            NewPassword = "StrongPass1!",
        });
}
