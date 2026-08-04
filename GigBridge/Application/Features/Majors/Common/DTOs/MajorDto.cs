namespace Application.Features.Majors.Common.DTOs;

public sealed record MajorDto(
    Guid MajorId,
    string Name,
    string Slug,
    string? Description,
    int? SortOrder
);
