using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Auth.ChangePassword.Commands;
using Application.Features.Auth.ChangePassword.DTOs;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth.ChangePassword;

public class ChangePasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPasswordChanges_RevokesExistingRefreshSession()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Test User",
            Email = "test.user@example.com",
            Password = "old-password-hash",
            RefreshTokenHash = "active-refresh-token-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            PreviousRefreshTokenHash = "previous-refresh-token-hash",
            PreviousRefreshTokenGraceExpiresAt = DateTime.UtcNow.AddSeconds(30)
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);

        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.VerifyPassword("current-password", user.Password).Returns(true);
        passwordHasher.HashPassword("new-password").Returns("new-password-hash");

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(user.UserId.ToString());

        var handler = new ChangePasswordCommandHandler(context, passwordHasher, currentUser);
        var command = new ChangePasswordCommand(new ChangePasswordProfileRequest
        {
            CurrentPassword = "current-password",
            NewPassword = "new-password"
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("new-password-hash", user.Password);
        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Null(user.PreviousRefreshTokenHash);
        Assert.Null(user.PreviousRefreshTokenGraceExpiresAt);
        Assert.Equal(1, context.SaveChangesCount);
    }
}
