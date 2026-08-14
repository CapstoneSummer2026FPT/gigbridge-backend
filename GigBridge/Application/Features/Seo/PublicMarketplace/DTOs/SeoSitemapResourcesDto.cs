namespace Application.Features.Seo.PublicMarketplace.DTOs;

public sealed record SeoSitemapEntryDto(Guid Id, DateTime LastModified);

public sealed record SeoSitemapResourcesDto(
    IReadOnlyList<SeoSitemapEntryDto> Jobs,
    IReadOnlyList<SeoSitemapEntryDto> Freelancers);
