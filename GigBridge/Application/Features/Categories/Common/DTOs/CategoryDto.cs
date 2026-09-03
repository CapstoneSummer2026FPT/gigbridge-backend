namespace Application.Features.Categories.Common.DTOs;

public sealed record CategoryDto(
    Guid CategoryId,
    string Name,
    string Slug,
    string? Description,
    int? SortOrder
);
