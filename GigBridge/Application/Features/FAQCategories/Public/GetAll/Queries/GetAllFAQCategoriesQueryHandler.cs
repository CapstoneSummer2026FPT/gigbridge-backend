using Application.Common.Interfaces;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.FAQCategories.GetAll.Queries;

public sealed class GetAllFAQCategoriesQueryHandler : IRequestHandler<GetAllFAQCategoriesQuery, IReadOnlyList<FAQCategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllFAQCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FAQCategoryDto>> Handle(GetAllFAQCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _context.Set<Faqcategory>()
            .Include(c => c.Faqs)
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(c => new FAQCategoryDto
        {
            Id = c.FaqcategoriesId,
            Name = c.Name,
            NameVi = c.NameVi,
            Slug = c.Slug,
            SortOrder = c.SortOrder,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            FaqCount = c.Faqs.Count(f => f.IsActive == true)
        }).ToList();
    }
}
