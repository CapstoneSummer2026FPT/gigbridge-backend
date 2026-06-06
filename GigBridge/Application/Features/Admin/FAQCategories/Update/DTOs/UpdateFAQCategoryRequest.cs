namespace Application.Features.Admin.FAQCategories.Update.DTOs;

public sealed record UpdateFAQCategoryRequest
{
    public string? Name { get; init; }
    public string? NameVi { get; init; }
    public string? Slug { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
}
