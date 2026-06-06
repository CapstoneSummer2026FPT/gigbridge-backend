using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQCategories.Update.Commands;

public sealed class UpdateFAQCategoryCommandHandler : IRequestHandler<UpdateFAQCategoryCommand, FAQCategoryDto?>
{
    private readonly IApplicationDbContext _context;

    public UpdateFAQCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQCategoryDto?> Handle(UpdateFAQCategoryCommand request, CancellationToken cancellationToken)
    {
        var categoryRequest = request.Request;

        var category = await _context.Set<Faqcategory>()
            .Include(c => c.Faqs)
            .FirstOrDefaultAsync(c => c.FaqcategoriesId == request.Id, cancellationToken);

        if (category is null)
            return null;

        if (categoryRequest.Name is not null)
            category.Name = categoryRequest.Name.Trim();

        if (categoryRequest.NameVi is not null)
            category.NameVi = categoryRequest.NameVi.Trim();

        if (categoryRequest.Slug is not null)
        {
            var slug = categoryRequest.Slug.Trim().ToLowerInvariant().Replace(" ", "-");
            var slugExists = await _context.Set<Faqcategory>()
                .AnyAsync(c => c.Slug == slug && c.FaqcategoriesId != request.Id, cancellationToken);

            if (slugExists)
                throw new ConflictException($"A category with slug '{slug}' already exists.");

            category.Slug = slug;
        }

        if (categoryRequest.SortOrder.HasValue)
            category.SortOrder = categoryRequest.SortOrder.Value;

        if (categoryRequest.IsActive.HasValue)
            category.IsActive = categoryRequest.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

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
