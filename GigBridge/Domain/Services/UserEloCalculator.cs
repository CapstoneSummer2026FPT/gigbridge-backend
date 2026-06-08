namespace Domain.Services;

public static class UserEloCalculator
{
    public const int DefaultPoints = 100;
    public const int MinimumPoints = 0;
    public const int InactivityPeriodDays = 14;
    public const int InactivityPenaltyPerPeriod = -50;
    public const int ReturnBonusPoints = 100;
    public const int JobCompletionBonusPoints = 20;

    public static int CalculateReviewEventDelta(int rating)
    {
        return CalculateCompletionDelta(rating) + CalculateReviewRatingDelta(rating);
    }

    public static int CalculateCompletionDelta(int rating)
    {
        ValidateRating(rating);
        return rating >= 3 ? JobCompletionBonusPoints : 0;
    }

    public static int CalculateReviewRatingDelta(int rating)
    {
        return rating switch
        {
            1 => -50,
            2 => -30,
            3 => 20,
            4 => 40,
            5 => 50,
            _ => throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.")
        };
    }

    public static int CalculateInactivityPenalty(DateTime lastActivityAt, DateTime now)
    {
        if (now <= lastActivityAt)
        {
            return 0;
        }

        var fullPeriods = (int)((now - lastActivityAt).TotalDays / InactivityPeriodDays);
        return fullPeriods * InactivityPenaltyPerPeriod;
    }

    public static int CalculateReturnBonus(DateTime lastActivityAt, DateTime now)
    {
        if (now <= lastActivityAt)
        {
            return 0;
        }

        return (now - lastActivityAt).TotalDays >= InactivityPeriodDays ? ReturnBonusPoints : 0;
    }

    public static int ApplyDelta(int currentPoints, int delta)
    {
        return Math.Max(MinimumPoints, currentPoints + delta);
    }

    private static void ValidateRating(int rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }
    }
}
