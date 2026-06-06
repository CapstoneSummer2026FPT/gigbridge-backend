using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.FAQs.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQs.Create.Commands;

public sealed class CreateFAQCommandHandler : IRequestHandler<CreateFAQCommand, FAQDto>
{
    private readonly IApplicationDbContext _context;

    public CreateFAQCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQDto> Handle(CreateFAQCommand request, CancellationToken cancellationToken)
    {
        var faqRequest = request.Request;

        var categoryExists = await _context.Set<Faqcategory>()
            .AnyAsync(c => c.FaqcategoriesId == faqRequest.FaqCategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException($"FAQ category with ID {faqRequest.FaqCategoryId} not found.");

        var faq = new Faq
        {
            FaqcategoriesId = faqRequest.FaqCategoryId,
            Question = faqRequest.Question.Trim(),
            QuestionVi = faqRequest.QuestionVi?.Trim(),
            Answer = faqRequest.Answer.Trim(),
            AnswerVi = faqRequest.AnswerVi?.Trim(),
            SortOrder = faqRequest.SortOrder ?? 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Faq>().Add(faq);
        await _context.SaveChangesAsync(cancellationToken);

        var category = await _context.Set<Faqcategory>()
            .FirstAsync(c => c.FaqcategoriesId == faqRequest.FaqCategoryId, cancellationToken);

        return new FAQDto
        {
            Id = faq.FaqsId,
            FaqCategoryId = faq.FaqcategoriesId,
            FaqCategoryName = category.Name,
            Question = faq.Question,
            QuestionVi = faq.QuestionVi,
            Answer = faq.Answer,
            AnswerVi = faq.AnswerVi,
            SortOrder = faq.SortOrder,
            IsActive = faq.IsActive,
            CreatedAt = faq.CreatedAt
        };
    }
}
