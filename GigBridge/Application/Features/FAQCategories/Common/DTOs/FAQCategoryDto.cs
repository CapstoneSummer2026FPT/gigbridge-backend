namespace Application.Features.FAQCategories.Shared.DTOs;

public sealed record FAQCategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameVi { get; init; }
    public string Slug { get; init; } = string.Empty;
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public int FaqCount { get; init; }
}
