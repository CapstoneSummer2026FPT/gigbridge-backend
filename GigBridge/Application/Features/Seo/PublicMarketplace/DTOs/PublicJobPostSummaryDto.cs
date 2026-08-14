namespace Application.Features.Seo.PublicMarketplace.DTOs;

/// <summary>
/// Minimal anonymous job-post contract used by search pages and crawlers.
/// Contact information, attachments, internal identifiers, and promotion data
/// are intentionally excluded.
/// </summary>
public sealed record PublicJobPostSummaryDto(
    Guid JobPostsId,
    string Title,
    string DescriptionPreview,
    string? MajorName,
    string? CategoryName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateTime CreatedAt,
    string? ClientFullName,
    List<string> SkillNames,
    List<string> CustomSkillNames);
