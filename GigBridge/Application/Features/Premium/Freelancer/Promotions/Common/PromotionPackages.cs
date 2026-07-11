using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.Common;

internal static class PromotionPackages
{
    public const string SettingKey = "premium.freelancer.promotion-packages";

    public static async Task<IReadOnlyList<PromotionPackageDto>> LoadAsync(
        IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var json = await context.Set<PlatformSetting>().AsNoTracking()
            .Where(item => item.Key == SettingKey)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            throw new BadRequestException($"Platform setting '{SettingKey}' is required.");

        try
        {
            var packages = JsonSerializer.Deserialize<List<PromotionPackageDto>>(
                json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (packages is null || packages.Count == 0 ||
                packages.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != packages.Count ||
                packages.Any(item => string.IsNullOrWhiteSpace(item.Id) ||
                                     string.IsNullOrWhiteSpace(item.Name) ||
                                     item.DurationDays <= 0 || item.TokenPrice <= 0 ||
                                     item.BoostWeight <= 0 || item.MaxQueuedCampaigns < 0))
                throw new BadRequestException($"Platform setting '{SettingKey}' is invalid.");

            return packages.OrderBy(item => item.SortOrder).ThenBy(item => item.TokenPrice).ToList();
        }
        catch (JsonException exception)
        {
            throw new BadRequestException($"Platform setting '{SettingKey}' is invalid: {exception.Message}");
        }
    }
}
