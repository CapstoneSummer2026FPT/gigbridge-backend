using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;

namespace Application.Features.Portfolios.Common;

internal static class PortfolioItemMapping
{
    public static PortfolioItemDto ToDto(this PortfolioItem item) => new()
    {
        PortfolioItemId = item.PortfolioItemsId,
        Title = item.Title,
        Description = item.Description,
        ProjectUrl = item.ProjectUrl,
        ImageUrl = item.ImageUrl,
        ProjectDate = item.ProjectDate?.ToString("yyyy-MM-dd")
    };

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
