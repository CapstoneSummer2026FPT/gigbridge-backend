namespace Application.Features.MajorCategories.Common.DTOs;

public sealed record MajorCategoryDto(
    Guid MajorCategoryId,
    Guid MajorId,
    string MajorName,
    Guid CategoryId,
    string CategoryName
);
