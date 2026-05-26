using Application.DTOs.Admin;
using Application.Features.Admin.Reviews.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminReviewService : AdminServiceBase, IAdminReviewService
{
    private readonly IMapper _mapper;

    public AdminReviewService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ReviewDto>> GetAllAsync(ReviewPageQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var reviews = DbContext.Set<Review>().AsNoTracking();
        if (query.IsVisible.HasValue)
        {
            reviews = reviews.Where(x => x.IsVisible == query.IsVisible);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            reviews = reviews.Where(x => x.Comment != null && x.Comment.ToLower().Contains(search));
        }

        reviews = FilterDates(reviews, query, x => x.CreatedAt);
        return await ToPageAsync(reviews.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<ReviewDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<ReviewDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<Review>().AsNoTracking().Where(x => x.ReviewsId == id)
            .ProjectTo<ReviewDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(Review), id);
    }

    public async Task<ReviewDto> SetVisibilityAsync(Guid id, ReviewVisibilityRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        if (!request.IsVisible && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw Invalid("reason", "A reason is required when hiding a review.");
        }

        var review = await DbContext.Set<Review>().SingleOrDefaultAsync(x => x.ReviewsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Review), id);
        var oldValues = new { review.IsVisible, review.UpdatedAt };
        review.IsVisible = request.IsVisible;
        review.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "ReviewVisibilityChanged", id, "Review", oldValues,
            new { review.IsVisible, review.UpdatedAt, request.Reason });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }
}

