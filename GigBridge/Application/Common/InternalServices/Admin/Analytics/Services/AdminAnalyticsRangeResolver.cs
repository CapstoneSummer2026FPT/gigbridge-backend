using Application.Common.Exceptions;
using Application.Common.InternalServices.Admin.Analytics.Models;

namespace Application.Common.InternalServices.Admin.Analytics.Services;
public static class AdminAnalyticsRangeResolver
{
    public const string TimeZoneId = "Asia/Ho_Chi_Minh";

    public static ResolvedAdminAnalyticsRange Resolve(AdminAnalyticsRangeRequest request, DateTime utcNow)
    {
        var zone = ResolveTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var period = request.Period.Trim().ToLowerInvariant();
        var anchor = request.Anchor ?? DateOnly.FromDateTime(nowLocal);
        DateOnly from;
        DateOnly toExclusive;

        switch (period)
        {
            case "month":
                from = new DateOnly(anchor.Year, anchor.Month, 1);
                toExclusive = from.AddMonths(1);
                break;
            case "quarter":
                var firstMonth = ((anchor.Month - 1) / 3) * 3 + 1;
                from = new DateOnly(anchor.Year, firstMonth, 1);
                toExclusive = from.AddMonths(3);
                break;
            case "year":
                from = new DateOnly(anchor.Year, 1, 1);
                toExclusive = from.AddYears(1);
                break;
            case "custom":
                if (request.From is null || request.To is null)
                    throw new BadRequestException("Custom analytics ranges require both from and to dates.");
                if (request.To < request.From)
                    throw new BadRequestException("The analytics to date must be on or after the from date.");
                if (request.To.Value.DayNumber - request.From.Value.DayNumber + 1 > 366)
                    throw new BadRequestException("Custom analytics ranges cannot exceed 366 days.");
                from = request.From.Value;
                toExclusive = request.To.Value.AddDays(1);
                break;
            default:
                throw new BadRequestException("Period must be month, quarter, year, or custom.");
        }

        var duration = toExclusive.DayNumber - from.DayNumber;
        var comparisonFrom = from.AddDays(-duration);
        return new ResolvedAdminAnalyticsRange(
            period,
            ToUtc(from, zone),
            ToUtc(toExclusive, zone),
            ToUtc(comparisonFrom, zone),
            ToUtc(from, zone),
            TimeZoneId,
            duration <= 62 ? "day" : duration <= 186 ? "week" : "month");
    }

    public static DateOnly Bucket(DateTime utc, ResolvedAdminAnalyticsRange range)
    {
        var local = ToLocal(utc);
        var date = DateOnly.FromDateTime(local);
        if (range.BucketGranularity == "month") return new DateOnly(date.Year, date.Month, 1);
        if (range.BucketGranularity == "week")
        {
            var offset = ((int)local.DayOfWeek + 6) % 7;
            return date.AddDays(-offset);
        }
        return date;
    }

    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    private static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ResolveTimeZone());

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}
