using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQCategories.ToggleActivity.Commands;

public sealed class ToggleFAQCategoryActivityCommandHandler : IRequestHandler<ToggleFAQCategoryActivityCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ToggleFAQCategoryActivityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ToggleFAQCategoryActivityCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Set<Faqcategory>()
            .FirstOrDefaultAsync(c => c.FaqcategoriesId == request.Id, cancellationToken);

        if (category is null)
            return false;

        category.IsActive = !(category.IsActive ?? true);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
