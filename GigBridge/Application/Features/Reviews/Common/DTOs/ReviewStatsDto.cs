namespace Application.Features.Reviews.Common.DTOs;

public class ReviewStatsDto
{
    public double AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public Dictionary<int, int> RatingDistribution { get; set; } = new()
    {
        [5] = 0,
        [4] = 0,
        [3] = 0,
        [2] = 0,
        [1] = 0
    };
}
