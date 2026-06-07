using Application.Common.Interfaces;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQCategories.GetAll.Queries;

public sealed class GetAllAdminFAQCategoriesQueryHandler : IRequestHandler<GetAllAdminFAQCategoriesQuery, IReadOnlyList<FAQCategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllAdminFAQCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FAQCategoryDto>> Handle(GetAllAdminFAQCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _context.Set<Faqcategory>()
            .Include(c => c.Faqs)
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
            FaqCount = c.Faqs.Count
        }).ToList();
    }
}
