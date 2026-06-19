using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Mappings;
using Application.Features.Admin.Users.CreateNewUser.Commands;
using Application.Features.Admin.Users.CreateNewUser.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Users;

public class CreateNewUserCommandHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_PersistsRequestedEmailVerificationState(bool isEmailVerified)
    {
        await using var context = CreateContext();
        var handler = new CreateNewUserCommandHandler(
            context,
            new StubPasswordHasher(),
            new FixedDateTimeService(),
            CreateMapper());

        var result = await handler.Handle(
            new CreateNewUserCommand(new CreateUserRequest
            {
                FullName = "Manual User",
                Email = "manual@example.com",
                Password = "password123",
                Role = (int)UserRole.Client,
                IsEmailVerified = isEmailVerified
            }),
            CancellationToken.None);

        var user = await context.Users.SingleAsync();
        Assert.Equal(isEmailVerified, user.IsEmailVerified);
        Assert.Equal(isEmailVerified, result.IsEmailVerified);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string password, string hashedPassword) => hashedPassword == $"hashed:{password}";
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public DateTime UtcNow => new(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
    }
}
