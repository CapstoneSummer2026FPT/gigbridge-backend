using Application.Common.Interfaces;
using Application.Features.Reviews.Common;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Common.GetReviewsByUser.Queries;

public class GetReviewsByUserQueryHandler
    : IRequestHandler<GetReviewsByUserQuery, IEnumerable<ReviewDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReviewsByUserQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ReviewDto>> Handle(
        GetReviewsByUserQuery request,
        CancellationToken cancellationToken)
    {
        var reviews = await _context.Set<Review>()
            .AsNoTracking()
            .Include(review => review.Contracts)
            .Include(review => review.Reviewer)
            .Where(review =>
                review.RevieweeId == request.UserId &&
                review.ModerationStatus == (int)ReviewModerationStatus.Active)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        return reviews.Select(ReviewProjection.ToDto);
    }
}
