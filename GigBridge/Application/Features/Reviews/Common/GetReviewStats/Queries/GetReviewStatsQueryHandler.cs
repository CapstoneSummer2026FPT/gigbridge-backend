using Application.Common.Interfaces;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Common.GetReviewStats.Queries;

public class GetReviewStatsQueryHandler : IRequestHandler<GetReviewStatsQuery, ReviewStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetReviewStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewStatsDto> Handle(
        GetReviewStatsQuery request,
        CancellationToken cancellationToken)
    {
        var ratings = await _context.Set<Review>()
            .AsNoTracking()
            .Where(review => review.RevieweeId == request.UserId)
            .Select(review => review.Rating)
            .ToListAsync(cancellationToken);

        var stats = new ReviewStatsDto
        {
            TotalReviews = ratings.Count,
            AverageRating = ratings.Count == 0
                ? 0
                : Math.Round(ratings.Average(), 1)
        };

        foreach (var rating in ratings)
        {
            stats.RatingDistribution[rating] = stats.RatingDistribution.GetValueOrDefault(rating) + 1;
        }

        return stats;
    }
}
