using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Features.Admin.Elo.Commands.UpdateEloPolicy;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Elo;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Elo;

public sealed class UpdateEloPolicyCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_CreatesSettingsWhenAbsent()
    {
        await using var context = CreateContext();
        var admin = AddAdmin(context);
        await context.SaveChangesAsync();
        var audit = Substitute.For<IAdminAuditService>();
        var handler = new UpdateEloPolicyCommandHandler(context, new Clock(), audit);

        var result = await handler.Handle(
            new UpdateEloPolicyCommand(admin.UserId, EloAdjustmentMode.FixedPoints, 80m),
            CancellationToken.None);

        Assert.Equal(EloAdjustmentMode.FixedPoints, result.Mode);
        Assert.Equal(80m, result.Value);

        var settings = await context.PlatformSettings.ToListAsync();
        Assert.Equal(2, settings.Count);
        Assert.Contains(settings, s => s.Key == EloPolicy.DisputePenaltyModeKey && s.Value == "FixedPoints");
        Assert.Contains(settings, s => s.Key == EloPolicy.DisputePenaltyValueKey && s.Value == "80");
        Assert.All(settings, s => Assert.Equal(admin.UserId, s.UpdatedByAdminId));

        audit.Received(1).Add(
            admin.UserId, "Elo.PolicyUpdate", nameof(PlatformSetting), null,
            Arg.Any<object>(), Arg.Any<EloPolicyDto>());
    }

    [Fact]
    public async Task Handle_UpdatesExistingSettings()
    {
        await using var context = CreateContext();
        var admin = AddAdmin(context);
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyModeKey,
            Value = "Percentage",
            DataType = "string"
        });
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyValueKey,
            Value = "50",
            DataType = "string"
        });
        await context.SaveChangesAsync();
        var handler = new UpdateEloPolicyCommandHandler(context, new Clock(), Substitute.For<IAdminAuditService>());

        await handler.Handle(
            new UpdateEloPolicyCommand(admin.UserId, EloAdjustmentMode.Percentage, 60m),
            CancellationToken.None);

        var settings = await context.PlatformSettings.ToListAsync();
        Assert.Equal(2, settings.Count);
        Assert.Contains(settings, s => s.Key == EloPolicy.DisputePenaltyValueKey && s.Value == "60");
    }

    [Fact]
    public async Task Handle_RejectsInvalidValue()
    {
        await using var context = CreateContext();
        var admin = AddAdmin(context);
        await context.SaveChangesAsync();
        var handler = new UpdateEloPolicyCommandHandler(context, new Clock(), Substitute.For<IAdminAuditService>());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateEloPolicyCommand(admin.UserId, EloAdjustmentMode.Percentage, 150m),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectsNonAdmin()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Client User",
            Email = $"{Guid.NewGuid():N}@client.com",
            Role = (int)UserRole.Client,
            IsActive = true,
            CreatedAt = Now
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var handler = new UpdateEloPolicyCommandHandler(context, new Clock(), Substitute.For<IAdminAuditService>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new UpdateEloPolicyCommand(user.UserId, EloAdjustmentMode.Percentage, 50m),
            CancellationToken.None));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static User AddAdmin(GigbridgeDbContext context)
    {
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin User",
            Email = $"{Guid.NewGuid():N}@admin.com",
            Role = (int)UserRole.Admin,
            IsActive = true,
            CreatedAt = Now
        };
        context.Users.Add(admin);
        return admin;
    }

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }
}
