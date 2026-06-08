using Application.Features.Auth.Register.Commands;
using Application.Features.Auth.Register.DTOs;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Application.Features.Auth.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new RegisterCommand(CreateValidRequest());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenEmailIsInvalid()
    {
        var request = CreateValidRequest();
        request.Email = "not-an-email";
        var command = new RegisterCommand(request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "RegisterRequest.Email");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenPasswordIsWeak()
    {
        var request = CreateValidRequest();
        request.Password = "password";
        request.ConfirmPassword = "password";
        var command = new RegisterCommand(request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "RegisterRequest.Password");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenConfirmPasswordDoesNotMatch()
    {
        var request = CreateValidRequest();
        request.ConfirmPassword = "OtherPass1!";
        var command = new RegisterCommand(request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "RegisterRequest.ConfirmPassword");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenRoleIsMissing()
    {
        var request = CreateValidRequest();
        request.role = null;
        var command = new RegisterCommand(request);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "RegisterRequest.role");
    }

    private static RegisterRequest CreateValidRequest()
    {
        return new RegisterRequest
        {
            Email = "client@example.com",
            FullName = "Client User",
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!",
            role = UserRole.Client
        };
    }
}
