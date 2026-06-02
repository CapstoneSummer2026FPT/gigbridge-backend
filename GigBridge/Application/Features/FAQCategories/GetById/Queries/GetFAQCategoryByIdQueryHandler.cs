using Application.Common.Interfaces;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.FAQCategories.GetById.Queries;

public sealed class GetFAQCategoryByIdQueryHandler : IRequestHandler<GetFAQCategoryByIdQuery, FAQCategoryDto?>
{
    private readonly IApplicationDbContext _context;

    public GetFAQCategoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQCategoryDto?> Handle(GetFAQCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _context.Set<Faqcategory>()
            .Include(c => c.Faqs)
            .FirstOrDefaultAsync(c => c.FaqcategoriesId == request.Id, cancellationToken);

        if (category is null)
            return null;

        return new FAQCategoryDto
        {
            Id = category.FaqcategoriesId,
            Name = category.Name,
            NameVi = category.NameVi,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            FaqCount = category.Faqs.Count
        };
    }
}
