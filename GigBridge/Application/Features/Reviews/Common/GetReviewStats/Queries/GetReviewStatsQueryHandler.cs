using Application.Common.Interfaces;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
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
            .Where(review =>
                review.RevieweeId == request.UserId &&
                review.ModerationStatus == (int)ReviewModerationStatus.Active)
            .Select(review => review.Rating)
            .ToListAsync(cancellationToken);

        var stats = new ReviewStatsDto
        {
            TotalReviews = ratings.Count,
            AverageRating = ratings.Count == 0
                ? 0
                : (double)Math.Round(ratings.Average(), 1)
        };

        foreach (var rating in ratings)
        {
            // Star-bucket distribution: 3.3 and 3.7 both land in the 3-star bucket.
            var bucket = (int)Math.Round(rating, 0, MidpointRounding.AwayFromZero);
            stats.RatingDistribution[bucket] = stats.RatingDistribution.GetValueOrDefault(bucket) + 1;
        }

        return stats;
    }
}
