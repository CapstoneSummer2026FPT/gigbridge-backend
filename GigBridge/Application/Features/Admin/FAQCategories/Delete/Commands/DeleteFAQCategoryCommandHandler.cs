using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQCategories.Delete.Commands;

public sealed class DeleteFAQCategoryCommandHandler : IRequestHandler<DeleteFAQCategoryCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteFAQCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteFAQCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Set<Faqcategory>()
            .Include(c => c.Faqs)
            .FirstOrDefaultAsync(c => c.FaqcategoriesId == request.Id, cancellationToken);

        if (category is null)
            throw new NotFoundException($"FAQ category with ID {request.Id} not found.");

        if (category.Faqs.Count > 0)
            throw new ConflictException(
                $"Cannot delete category '{category.Name}' because it has {category.Faqs.Count} associated FAQ(s). Remove or reassign them first.");

        _context.Set<Faqcategory>().Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
