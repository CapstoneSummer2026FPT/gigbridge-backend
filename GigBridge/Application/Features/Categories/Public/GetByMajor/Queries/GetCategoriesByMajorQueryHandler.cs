using Application.Common.Interfaces;
using Application.Features.Categories.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Public.GetByMajor.Queries;

public sealed class GetCategoriesByMajorQueryHandler
    : IRequestHandler<GetCategoriesByMajorQuery, IReadOnlyList<CategoryOptionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriesByMajorQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryOptionDto>> Handle(
        GetCategoriesByMajorQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Set<MajorCategory>()
            .AsNoTracking()
            .Where(majorCategory =>
                majorCategory.MajorId == request.MajorId &&
                majorCategory.Major.IsActive &&
                majorCategory.Category.IsActive)
            .OrderBy(majorCategory => majorCategory.Category.SortOrder)
            .ThenBy(majorCategory => majorCategory.Category.Name)
            .Select(majorCategory => new CategoryOptionDto(
                majorCategory.MajorCategoriesId,
                majorCategory.CategoryId,
                majorCategory.Category.Name,
                majorCategory.Category.Slug,
                majorCategory.Category.Description,
                majorCategory.Category.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
