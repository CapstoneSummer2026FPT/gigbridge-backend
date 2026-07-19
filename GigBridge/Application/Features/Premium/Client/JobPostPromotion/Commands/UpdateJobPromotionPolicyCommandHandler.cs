using System.Globalization;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Client.JobPostPromotion.Common;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class UpdateJobPromotionPolicyCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock) : IRequestHandler<UpdateJobPromotionPolicyCommand, JobPromotionPolicyDto>
{
    public async Task<JobPromotionPolicyDto> Handle(
        UpdateJobPromotionPolicyCommand command,
        CancellationToken cancellationToken)
    {
        JobPromotionPolicy.Validate(command.Request.TokenCost, command.Request.DurationDays);
        var settings = await context.Set<PlatformSetting>()
            .Where(x => x.Key == JobPromotionPolicy.TokenCostKey ||
                x.Key == JobPromotionPolicy.DurationDaysKey)
            .ToListAsync(cancellationToken);
        Upsert(settings, JobPromotionPolicy.TokenCostKey,
            command.Request.TokenCost.ToString(CultureInfo.InvariantCulture), command.AdminUserId, clock.UtcNow);
        Upsert(settings, JobPromotionPolicy.DurationDaysKey,
            command.Request.DurationDays.ToString(CultureInfo.InvariantCulture), command.AdminUserId, clock.UtcNow);
        foreach (var setting in settings.Where(x => x.PlatformSettingsId == Guid.Empty))
        {
            setting.PlatformSettingsId = Guid.NewGuid();
            context.Set<PlatformSetting>().Add(setting);
        }
        await context.SaveChangesAsync(cancellationToken);
        return new JobPromotionPolicyDto(command.Request.TokenCost, command.Request.DurationDays);
    }

    private static void Upsert(
        ICollection<PlatformSetting> settings,
        string key,
        string value,
        Guid adminUserId,
        DateTime now)
    {
        var setting = settings.FirstOrDefault(x => x.Key == key);
        if (setting is null)
        {
            settings.Add(new PlatformSetting
            {
                Key = key,
                Value = value,
                DataType = "number",
                Description = "Premium Client job promotion policy",
                UpdatedAt = now,
                UpdatedByAdminId = adminUserId
            });
            return;
        }
        setting.Value = value;
        setting.UpdatedAt = now;
        setting.UpdatedByAdminId = adminUserId;
    }
}
