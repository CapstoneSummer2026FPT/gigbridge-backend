using Application.Common.Interfaces;
using Application.Features.MajorCategories.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MajorCategories.Public.GetAll.Queries;

public sealed class GetAllMajorCategoriesQueryHandler
    : IRequestHandler<GetAllMajorCategoriesQuery, IReadOnlyList<MajorCategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMajorCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MajorCategoryDto>> Handle(
        GetAllMajorCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Set<MajorCategory>()
            .AsNoTracking()
            .Where(majorCategory =>
                majorCategory.Major.IsActive &&
                majorCategory.Category.IsActive)
            .OrderBy(majorCategory => majorCategory.Major.SortOrder)
            .ThenBy(majorCategory => majorCategory.Major.Name)
            .ThenBy(majorCategory => majorCategory.Category.SortOrder)
            .ThenBy(majorCategory => majorCategory.Category.Name)
            .Select(majorCategory => new MajorCategoryDto(
                majorCategory.MajorCategoriesId,
                majorCategory.MajorId,
                majorCategory.Major.Name,
                majorCategory.CategoryId,
                majorCategory.Category.Name))
            .ToListAsync(cancellationToken);
    }
}
