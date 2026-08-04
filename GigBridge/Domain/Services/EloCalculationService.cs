namespace Domain.Services;

/// <summary>
/// Calculates the Elo change awarded to the reviewed party of a completed job.
///
/// The delta is a single piecewise-linear function of the review's overall
/// rating (decimal, 1.0–5.0, one decimal place). The mapping reproduces the
/// reference table exactly:
///   1.0→-50, 1.5→-35, 2.0→-20, 2.5→-15, 3.0→-10, 3.2→-2, 3.3→+2,
///   3.5→+10, 4.0→+30, 4.5→+40, 5.0→+50
///
/// Calculation logic lives here (domain service), never in a controller or
/// command handler.
/// </summary>
public static class EloCalculationService
{
    public const decimal MinimumRating = 1.0m;
    public const decimal MaximumRating = 5.0m;

    /// <summary>
    /// Returns the Elo change for a completed-job final rating.
    /// The rating must be within 1.0–5.0 and carry at most one decimal place.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rating"/> is outside 1.0–5.0 or has more
    /// than one decimal place.
    /// </exception>
    public static int CalculateEloChange(decimal rating)
    {
        EnsureValidRating(rating);

        var change = rating switch
        {
            <= 2.0m => -50m + 30m * (rating - 1.0m),
            <= 3.0m => -20m + 10m * (rating - 2.0m),
            <= 4.0m => -10m + 40m * (rating - 3.0m),
            _ => 30m + 20m * (rating - 4.0m)
        };

        return (int)Math.Round(change, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// True when the rating is within 1.0–5.0 and has at most one decimal place.
    /// </summary>
    public static bool IsValidRating(decimal rating)
    {
        if (rating < MinimumRating || rating > MaximumRating)
        {
            return false;
        }

        return rating == Math.Round(rating, 1);
    }

    /// <summary>
    /// Throws for out-of-range ratings or ratings with more than one decimal place.
    /// </summary>
    public static void EnsureValidRating(decimal rating)
    {
        if (!IsValidRating(rating))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                rating,
                "Rating must be between 1.0 and 5.0 with at most one decimal place.");
        }
    }
}
