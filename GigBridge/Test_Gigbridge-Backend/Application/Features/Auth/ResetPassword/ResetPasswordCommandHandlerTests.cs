using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Exceptions;
using Application.Features.Auth.ResetPassword.Commands;
using Application.Features.Auth.ResetPassword.DTOs;
using Application.Features.Auth.Common;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth.ResetPassword;

public class ResetPasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPasswordResets_RevokesExistingRefreshSession()
    {
        // Arrange
        const string normalizedEmail = "test.user@example.com";
        const string otp = "123456";
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Test User",
            Email = normalizedEmail,
            Password = "old-password-hash",
            RefreshTokenHash = "active-refresh-token-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);

        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.HashPassword("new-password").Returns("new-password-hash");

        var cache = Substitute.For<ICacheService>();
        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.PasswordReset,
            normalizedEmail,
            otp);
        cache.GetAndRemoveAsync<bool>(verificationKey, CancellationToken.None)
            .Returns(true);

        var handler = new ResetPasswordCommandHandler(context, passwordHasher, cache);
        var command = new ResetPasswordCommand(new ResetPasswordRequest
        {
            Email = "  TEST.USER@EXAMPLE.COM ",
            NewPassword = "new-password",
            Otp = otp
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("new-password-hash", user.Password);
        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Equal(1, context.SaveChangesCount);
        await cache.Received(1)
            .GetAndRemoveAsync<bool>(verificationKey, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_UsesSameErrorForUnknownEmailAndMissingVerification()
    {
        const string otp = "123456";
        const string unknownEmail = "unknown@example.com";
        var context = new InMemoryApplicationDbContext();
        context.AddSet<User>();
        var cache = Substitute.For<ICacheService>();
        cache.GetAndRemoveAsync<bool>(
                OtpSecurity.VerifiedKey(OtpPurpose.PasswordReset, unknownEmail, otp),
                CancellationToken.None)
            .Returns(true);
        var handler = new ResetPasswordCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            cache);
        var request = new ResetPasswordRequest
        {
            Email = unknownEmail,
            NewPassword = "StrongPass1!",
            Otp = otp
        };

        var unknownEmailException = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new ResetPasswordCommand(request), CancellationToken.None));

        cache.GetAndRemoveAsync<bool>(
                OtpSecurity.VerifiedKey(OtpPurpose.PasswordReset, unknownEmail, otp),
                CancellationToken.None)
            .Returns(false);
        var missingVerificationException = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new ResetPasswordCommand(request), CancellationToken.None));

        Assert.Equal(missingVerificationException.Message, unknownEmailException.Message);
        Assert.Equal("Invalid or expired OTP verification code.", unknownEmailException.Message);
    }
}
