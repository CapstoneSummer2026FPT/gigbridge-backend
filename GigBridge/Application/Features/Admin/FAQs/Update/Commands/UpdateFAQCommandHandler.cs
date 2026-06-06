using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.FAQs.Shared.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQs.Update.Commands;

public sealed class UpdateFAQCommandHandler : IRequestHandler<UpdateFAQCommand, FAQDto?>
{
    private readonly IApplicationDbContext _context;

    public UpdateFAQCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FAQDto?> Handle(UpdateFAQCommand request, CancellationToken cancellationToken)
    {
        var faqRequest = request.Request;

        var faq = await _context.Set<Faq>()
            .FirstOrDefaultAsync(f => f.FaqsId == request.Id, cancellationToken);

        if (faq is null)
            throw new NotFoundException($"FAQ with ID {request.Id} not found.");
        if (faqRequest.FaqCategoryId.HasValue)
        {
            var categoryExists = await _context.Set<Faqcategory>()
                .AnyAsync(c => c.FaqcategoriesId == faqRequest.FaqCategoryId.Value, cancellationToken);

            if (!categoryExists)
                throw new NotFoundException($"FAQ category with ID {faqRequest.FaqCategoryId.Value} not found.");

            faq.FaqcategoriesId = faqRequest.FaqCategoryId.Value;
        }

        if (faqRequest.Question is not null)
            faq.Question = faqRequest.Question.Trim();

        if (faqRequest.QuestionVi is not null)
            faq.QuestionVi = faqRequest.QuestionVi.Trim();

        if (faqRequest.Answer is not null)
            faq.Answer = faqRequest.Answer.Trim();

        if (faqRequest.AnswerVi is not null)
            faq.AnswerVi = faqRequest.AnswerVi.Trim();

        if (faqRequest.SortOrder.HasValue)
            faq.SortOrder = faqRequest.SortOrder.Value;

        if (faqRequest.IsActive.HasValue)
            faq.IsActive = faqRequest.IsActive.Value;

        faq.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var category = await _context.Set<Faqcategory>()
            .FirstOrDefaultAsync(c => c.FaqcategoriesId == faq.FaqcategoriesId, cancellationToken);

        if (category is null)
            throw new NotFoundException($"FAQ category with ID {faq.FaqcategoriesId} not found.");

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
            CreatedAt = faq.CreatedAt,
            UpdatedAt = faq.UpdatedAt
        };
    }
}
