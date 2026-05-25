using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs.Admin;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminFaqService : AdminServiceBase, IAdminFaqService
{
    private readonly IMapper _mapper;

    public AdminFaqService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<FaqDto>> GetAllAsync(FaqPageQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var faqs = DbContext.Set<Faq>().AsNoTracking();
        if (query.CategoryId.HasValue)
        {
            faqs = faqs.Where(x => x.FaqcategoriesId == query.CategoryId);
        }

        if (query.IsActive.HasValue)
        {
            faqs = faqs.Where(x => x.IsActive == query.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            faqs = faqs.Where(x => x.Question.ToLower().Contains(search) || x.Answer.ToLower().Contains(search));
        }

        faqs = FilterDates(faqs, query, x => x.CreatedAt);
        return await ToPageAsync(faqs.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ProjectTo<FaqDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<FaqDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<Faq>().AsNoTracking().Where(x => x.FaqsId == id)
            .ProjectTo<FaqDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(Faq), id);
    }

    public async Task<FaqDto> CreateAsync(SaveFaqRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        Validate(request);
        await RequireCategoryAsync(request.CategoryId, cancellationToken);
        var faq = new Faq
        {
            FaqsId = Guid.NewGuid(),
            FaqcategoriesId = request.CategoryId,
            Question = request.Question!.Trim(),
            QuestionVi = request.QuestionVi,
            Answer = request.Answer!.Trim(),
            AnswerVi = request.AnswerVi,
            SortOrder = request.SortOrder ?? 0,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Set<Faq>().Add(faq);
        AddAudit(actor, "FaqCreated", faq.FaqsId, "Faq", null, Values(faq));
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(faq.FaqsId, cancellationToken);
    }

    public async Task<FaqDto> UpdateAsync(Guid id, SaveFaqRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        Validate(request);
        await RequireCategoryAsync(request.CategoryId, cancellationToken);
        var faq = await DbContext.Set<Faq>().SingleOrDefaultAsync(x => x.FaqsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Faq), id);
        var oldValues = Values(faq);
        faq.FaqcategoriesId = request.CategoryId;
        faq.Question = request.Question!.Trim();
        faq.QuestionVi = request.QuestionVi;
        faq.Answer = request.Answer!.Trim();
        faq.AnswerVi = request.AnswerVi;
        faq.SortOrder = request.SortOrder ?? 0;
        faq.IsActive = request.IsActive ?? true;
        faq.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "FaqUpdated", id, "Faq", oldValues, Values(faq));
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var faq = await DbContext.Set<Faq>().SingleOrDefaultAsync(x => x.FaqsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Faq), id);
        var oldValues = Values(faq);
        DbContext.Set<Faq>().Remove(faq);
        AddAudit(actor, "FaqDeleted", id, "Faq", oldValues, null);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == Guid.Empty || !await DbContext.Set<Faqcategory>().AnyAsync(x => x.FaqcategoriesId == categoryId, cancellationToken))
        {
            throw Invalid("categoryId", "The FAQ category does not exist.");
        }
    }

    private static void Validate(SaveFaqRequestDto request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            errors["question"] = ["Question is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            errors["answer"] = ["Answer is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static object Values(Faq faq) => new
    {
        FaqId = faq.FaqsId,
        CategoryId = faq.FaqcategoriesId,
        faq.Question,
        faq.QuestionVi,
        faq.Answer,
        faq.AnswerVi,
        faq.SortOrder,
        faq.IsActive,
        faq.CreatedAt,
        faq.UpdatedAt
    };

}
