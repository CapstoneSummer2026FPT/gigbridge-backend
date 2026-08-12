using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Elo.Common.Interfaces;
using Application.Features.Auth.Register.Commands;
using Application.Features.Auth.Register.DTOs;
using AutoMapper;
using Domain.Enums.Accounts;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Auth.Register;

public class RegisterCommandHandlerSecurityTests
{
    [Fact]
    public async Task Handle_RejectsAdminRoleBeforeAccessingRegistrationDependencies()
    {
        // Arrange
        var context = Substitute.For<IApplicationDbContext>();
        var handler = new RegisterCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IDateTimeService>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = "admin-attempt@example.com",
            FullName = "Admin Attempt",
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!",
            VerificationTicket = new string('a', 64),
            role = UserRole.Admin
        });

        // Act
        var action = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(action);
        context.DidNotReceive().Set<Domain.Entities.User>();
    }
}
