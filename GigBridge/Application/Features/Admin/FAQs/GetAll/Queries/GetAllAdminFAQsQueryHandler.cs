using Application.Common.Interfaces;
using Application.Features.FAQs.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQs.GetAll.Queries;

public sealed class GetAllAdminFAQsQueryHandler : IRequestHandler<GetAllAdminFAQsQuery, IReadOnlyList<FAQDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllAdminFAQsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FAQDto>> Handle(GetAllAdminFAQsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Set<Faq>()
            .Include(f => f.Faqcategories)
            .AsQueryable();

        if (request.FaqCategoryId.HasValue)
            query = query.Where(f => f.FaqcategoriesId == request.FaqCategoryId.Value);

        var faqs = await query
            .OrderBy(f => f.Faqcategories.SortOrder)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.FaqsId)
            .ToListAsync(cancellationToken);

        return faqs.Select(f => new FAQDto
        {
            Id = f.FaqsId,
            FaqCategoryId = f.FaqcategoriesId,
            FaqCategoryName = f.Faqcategories?.Name,
            Question = f.Question,
            QuestionVi = f.QuestionVi,
            Answer = f.Answer,
            AnswerVi = f.AnswerVi,
            SortOrder = f.SortOrder,
            IsActive = f.IsActive,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();
    }
}
