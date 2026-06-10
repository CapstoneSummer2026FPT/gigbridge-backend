using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Auth.ForgotPassword.Commands;
using Application.Features.Auth.ForgotPassword.DTOs;
using Application.Features.Auth.GoogleLogin.Commands;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Login.Commands;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.RefreshToken.Commands;
using Application.Features.Auth.RefreshToken.DTOs;
using Application.Features.Auth.Register.Commands;
using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.ResetPassword.Commands;
using Application.Features.Auth.ResetPassword.DTOs;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.ValidateToken.Commands;
using Application.Features.Auth.ValidateToken.DTOs;
using Application.Features.Auth.SendOtp.Commands;
using Application.Features.Auth.SendOtp.DTOs;
using Application.Features.Auth.VerifyOtp.Commands;
using Application.Features.Auth.VerifyOtp.DTOs;
using Application.Features.Auth.ChangePassword.Commands;
using Application.Features.Auth.ChangePassword.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Project_API.Controllers;
using Xunit;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Project_API.Controllers;

public class AuthControllerTests
{
    private (AuthController Controller, IMediator Mediator, DefaultHttpContext HttpContext) CreateController()
    {
        var mediator = Substitute.For<IMediator>();
        var services = new ServiceCollection();
        services.AddSingleton(mediator);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var controller = new AuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return (controller, mediator, httpContext);
    }

    //[MethodName]_[Scenario]_[ExpectedResult]

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Arrange
        var (controller, _, _) = CreateController();

        // Act
        var result = await controller.Register(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenRegistrationSucceeds()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            role = UserRole.Freelancer
        };
        var userDto = new UserDTO
        {
            UserId = Guid.NewGuid(),
            Email = "test@test.com",
            Role = 1,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        mediator.Send(Arg.Any<RegisterCommand>()).Returns(userDto);

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDTO>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(userDto, apiResponse.Data);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRegistrationFails()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            role = UserRole.Freelancer
        };

        mediator.Send(Arg.Any<RegisterCommand>()).Returns((UserDTO)null!);

        // Act
        var result = await controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task SendOtp_ReturnsBadRequest_WhenEmailIsEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new SendOtpRequest { Email = "" };

        // Act
        var result = await controller.SendOtp(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task SendOtp_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new SendOtpRequest { Email = "test@test.com" };

        // Act
        var result = await controller.SendOtp(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        await mediator.Received(1).Send(Arg.Any<SendOtpCommand>());
    }

    [Fact]
    public async Task VerifyOtp_ReturnsBadRequest_WhenRequestIsInvalid()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new VerifyOtpRequest { Email = "", Otp = "123456" };

        // Act
        var result = await controller.VerifyOtp(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task VerifyOtp_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new VerifyOtpRequest { Email = "test@test.com", Otp = "123456" };

        // Act
        var result = await controller.VerifyOtp(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        await mediator.Received(1).Send(Arg.Any<VerifyOtpCommand>());
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Arrange
        var (controller, _, _) = CreateController();

        // Act
        var result = await controller.Login(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenLoginSucceeds()
    {
        // Arrange
        var (controller, mediator, httpContext) = CreateController();
        var request = new LoginRequest { Email = "test@test.com", Password = "Password123!" };
        var loginResponse = new LoginResponse
        {
            User = new UserDTO { UserId = Guid.NewGuid(), Email = "test@test.com", Role = 1 },
            Token = "jwt_token_here",
            refreshToken = "refresh_token_here"
        };
        var expiry = DateTime.UtcNow.AddDays(7);

        mediator.Send(Arg.Any<LoginWithRefreshCommand>()).Returns((loginResponse, "refreshtokenvalue", expiry));

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<LoginResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(loginResponse, apiResponse.Data);
        Assert.True(httpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenLoginFails()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new LoginRequest { Email = "test@test.com", Password = "Password123!" };

        mediator.Send(Arg.Any<LoginWithRefreshCommand>()).Returns(((LoginResponse)null!, "", DateTime.MinValue));

        // Act
        var result = await controller.Login(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task GoogleLogin_ReturnsBadRequest_WhenAuthCodeIsEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new GoogleLoginRequest { AuthCode = "", Role = 1, IsFromSignIn = true };

        // Act
        var result = await controller.GoogleLogin(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task GoogleLogin_ReturnsOk_WhenLoginSucceeds()
    {
        // Arrange
        var (controller, mediator, httpContext) = CreateController();
        var request = new GoogleLoginRequest { AuthCode = "googlecode", Role = 1, IsFromSignIn = true };
        var loginResponse = new LoginResponse
        {
            User = new UserDTO { UserId = Guid.NewGuid(), Email = "test@test.com", Role = 1 },
            Token = "jwt_token_here"
        };
        var expiry = DateTime.UtcNow.AddDays(7);

        mediator.Send(Arg.Any<GoogleLoginCommand>()).Returns((loginResponse, "refreshtokenvalue", expiry));

        // Act
        var result = await controller.GoogleLogin(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<LoginResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(loginResponse, apiResponse.Data);
        Assert.True(httpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenCookieIsMissing()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new TokenRequest { AccessToken = "accesstoken" };

        // Act
        var result = await controller.Refresh(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Refresh_ReturnsOk_WhenTokenRefreshedSuccessfully()
    {
        // Arrange
        var (controller, mediator, httpContext) = CreateController();
        var request = new TokenRequest { AccessToken = "accesstoken" };
        httpContext.Request.Headers.Append("Cookie", "refreshToken=oldrefreshtoken");

        var loginResponse = new LoginResponse
        {
            User = new UserDTO { UserId = Guid.NewGuid(), Email = "test@test.com", Role = 1 },
            Token = "new_jwt_token"
        };
        var expiry = DateTime.UtcNow.AddDays(7);

        mediator.Send(Arg.Any<RefreshTokenCommand>()).Returns((loginResponse, "newrefreshtoken", expiry));

        // Act
        var result = await controller.Refresh(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<LoginResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(loginResponse, apiResponse.Data);
        Assert.True(httpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Arrange
        var (controller, _, _) = CreateController();

        // Act
        var result = await controller.ChangePassword(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenFieldsAreEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new ChangePasswordProfileRequest { CurrentPassword = "", NewPassword = "newpassword" };

        // Act
        var result = await controller.ChangePassword(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task ChangePassword_ReturnsOk_WhenPasswordChangedSuccessfully()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new ChangePasswordProfileRequest { CurrentPassword = "oldpassword", NewPassword = "newpassword" };

        // Act
        var result = await controller.ChangePassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        await mediator.Received(1).Send(Arg.Any<ChangePasswordCommand>());
    }

    [Fact]
    public async Task SendPasswordEmailChanging_ReturnsBadRequest_WhenEmailIsEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new ForgotPasswordRequest { Email = "" };

        // Act
        var result = await controller.SendPasswordEmailChanging(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task SendPasswordEmailChanging_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new ForgotPasswordRequest { Email = "test@test.com" };

        // Act
        var result = await controller.SendPasswordEmailChanging(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        await mediator.Received(1).Send(Arg.Any<SendEmailPasswordChangingCommand>());
    }

    [Fact]
    public async Task PasswordChangingRequest_ReturnsBadRequest_WhenEmailIsEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new ResetPasswordRequest { Email = "", NewPassword = "newpassword", Otp = "123456" };

        // Act
        var result = await controller.PasswordChangingRequest(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task PasswordChangingRequest_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new ResetPasswordRequest { Email = "test@test.com", NewPassword = "newpassword", Otp = "123456" };

        // Act
        var result = await controller.PasswordChangingRequest(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        await mediator.Received(1).Send(Arg.Any<ResetPasswordCommand>());
    }

    [Fact]
    public async Task ValidateResetToken_ReturnsBadRequest_WhenTokenIsEmpty()
    {
        // Arrange
        var (controller, _, _) = CreateController();
        var request = new ValidateResetTokenRequest { Token = "" };

        // Act
        var result = await controller.ValidateResetToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task ValidateResetToken_ReturnsBadRequest_WhenTokenIsExpired()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new ValidateResetTokenRequest { Token = "expiredtoken" };
        mediator.Send(Arg.Any<ValidateResetTokenCommand>()).Returns(true); // expired

        // Act
        var result = await controller.ValidateResetToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Equal("TOKEN_EXPIRED", apiResponse.Message);
    }

    [Fact]
    public async Task ValidateResetToken_ReturnsOk_WhenTokenIsValid()
    {
        // Arrange
        var (controller, mediator, _) = CreateController();
        var request = new ValidateResetTokenRequest { Token = "validtoken" };
        mediator.Send(Arg.Any<ValidateResetTokenCommand>()).Returns(false); // not expired

        // Act
        var result = await controller.ValidateResetToken(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal("valid", apiResponse.Message);
    }
}
