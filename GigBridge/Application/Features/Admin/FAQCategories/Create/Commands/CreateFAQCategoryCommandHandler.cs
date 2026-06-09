using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQCategories.Create.Commands;

public sealed class CreateFAQCategoryCommandHandler : IRequestHandler<CreateFAQCategoryCommand, FAQCategoryDto>
{
    private readonly IApplicationDbContext _context;

    public CreateFAQCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQCategoryDto> Handle(CreateFAQCategoryCommand request, CancellationToken cancellationToken)
    {
        var categoryRequest = request.Request;
        var slug = categoryRequest.Slug.Trim().ToLowerInvariant().Replace(" ", "-");

        var existing = await _context.Set<Faqcategory>()
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

        if (existing is not null)
            throw new ConflictException($"A category with slug '{slug}' already exists.");

        var category = new Faqcategory
        {
            Name = categoryRequest.Name.Trim(),
            Slug = slug,
            SortOrder = categoryRequest.SortOrder ?? 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Faqcategory>().Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return new FAQCategoryDto
        {
            Id = category.FaqcategoriesId,
            Name = category.Name,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            FaqCount = 0
        };
    }
}
