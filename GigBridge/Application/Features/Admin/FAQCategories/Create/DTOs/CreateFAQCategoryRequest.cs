namespace Application.Features.Admin.FAQCategories.Create.DTOs;

public sealed record CreateFAQCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? NameVi { get; init; }
    public string Slug { get; init; } = string.Empty;
    public int? SortOrder { get; init; }
}
