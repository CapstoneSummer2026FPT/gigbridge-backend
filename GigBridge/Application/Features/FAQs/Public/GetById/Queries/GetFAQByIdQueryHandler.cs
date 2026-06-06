using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.FAQs.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.FAQs.GetById.Queries;

public sealed class GetFAQByIdQueryHandler : IRequestHandler<GetFAQByIdQuery, FAQDto?>
{
    private readonly IApplicationDbContext _context;

    public GetFAQByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQDto?> Handle(GetFAQByIdQuery request, CancellationToken cancellationToken)
    {
        var faq = await _context.Set<Faq>()
            .Include(f => f.Faqcategories)
            .Where(f => f.IsActive == true && f.Faqcategories.IsActive == true)
            .FirstOrDefaultAsync(f => f.FaqsId == request.Id, cancellationToken);

        if (faq is null)
            throw new NotFoundException($"FAQ with ID {request.Id} not found.");

        return new FAQDto
        {
            Id = faq.FaqsId,
            FaqCategoryId = faq.FaqcategoriesId,
            FaqCategoryName = faq.Faqcategories?.Name,
            Question = faq.Question,
            QuestionVi = faq.QuestionVi,
            Answer = faq.Answer,
            AnswerVi = faq.AnswerVi,
            SortOrder = faq.SortOrder,
            IsActive = faq.IsActive,
            CreatedAt = faq.CreatedAt,
            UpdatedAt = faq.UpdatedAt
        };
    }
}
