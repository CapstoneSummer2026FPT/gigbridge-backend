using Application.Common.Exceptions;
using Application.Features.Elo.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Services;

public sealed class EloPolicyTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenNothingConfigured()
    {
        await using var context = CreateContext();
        await context.SaveChangesAsync();

        var policy = await EloPolicy.LoadAsync(context, CancellationToken.None);

        Assert.Equal(EloAdjustmentMode.Percentage, policy.Mode);
        Assert.Equal(50m, policy.Value);
    }

    [Fact]
    public async Task LoadAsync_ParsesConfiguredRows()
    {
        await using var context = CreateContext();
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyModeKey,
            Value = "FixedPoints",
            DataType = "string",
            UpdatedAt = DateTime.UtcNow
        });
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyValueKey,
            Value = "80",
            DataType = "string",
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var policy = await EloPolicy.LoadAsync(context, CancellationToken.None);

        Assert.Equal(EloAdjustmentMode.FixedPoints, policy.Mode);
        Assert.Equal(80m, policy.Value);
    }

    [Fact]
    public async Task LoadAsync_ThrowsOnInvalidModeValue()
    {
        await using var context = CreateContext();
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyModeKey,
            Value = "Banana",
            DataType = "string"
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            EloPolicy.LoadAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_ThrowsOnInvalidNumericValue()
    {
        await using var context = CreateContext();
        context.PlatformSettings.Add(new PlatformSetting
        {
            PlatformSettingsId = Guid.NewGuid(),
            Key = EloPolicy.DisputePenaltyValueKey,
            Value = "not-a-number",
            DataType = "string"
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            EloPolicy.LoadAsync(context, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.5)]
    [InlineData(101)]
    public void Validate_RejectsOutOfRangePercentage(decimal value)
    {
        Assert.Throws<BadRequestException>(() => EloPolicy.Validate(EloAdjustmentMode.Percentage, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.5)]
    public void Validate_RejectsInvalidFixedPoints(decimal value)
    {
        Assert.Throws<BadRequestException>(() => EloPolicy.Validate(EloAdjustmentMode.FixedPoints, value));
    }

    [Fact]
    public void Validate_AcceptsBoundaryValues()
    {
        EloPolicy.Validate(EloAdjustmentMode.Percentage, 1);
        EloPolicy.Validate(EloAdjustmentMode.Percentage, 100);
        EloPolicy.Validate(EloAdjustmentMode.FixedPoints, 1);
        EloPolicy.Validate(EloAdjustmentMode.FixedPoints, 500);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }
}
