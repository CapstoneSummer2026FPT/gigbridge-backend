namespace Application.Features.Categories.Common.DTOs;

public sealed record CategoryOptionDto(
    Guid MajorCategoryId,
    Guid CategoryId,
    string Name,
    string Slug,
    string? Description,
    int? SortOrder
);
