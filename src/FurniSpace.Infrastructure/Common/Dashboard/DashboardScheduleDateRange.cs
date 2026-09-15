namespace FurniSpace.Infrastructure.Common.Dashboard;

/// <summary>
/// Resolves dashboard <c>dateRange</c> bounds in Asia/Ho_Chi_Minh, returned as UTC
/// half-open intervals: <c>[fromUtc, toUtcExclusive)</c>.
/// </summary>
internal static class DashboardScheduleDateRange
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    /// <summary>
    /// Returns UTC bounds for filtering <c>scheduledStart</c>, or null when dateRange is empty
    /// (no time filter).
    /// <list type="bullet">
    /// <item><c>today</c>: local calendar day</item>
    /// <item><c>thisWeek</c>: Monday 00:00 through Sunday end (next Monday exclusive)</item>
    /// <item><c>thisMonth</c>: 1st 00:00 through end of month (1st next month exclusive)</item>
    /// </list>
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtcExclusive)? TryResolve(string? dateRange, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return null;
        }

        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
        var localToday = DateOnly.FromDateTime(localNow);
        var range = dateRange.Trim();

        if (string.Equals(range, "today", StringComparison.OrdinalIgnoreCase))
        {
            return ToUtcHalfOpen(localToday, localToday.AddDays(1));
        }

        if (string.Equals(range, "thisWeek", StringComparison.OrdinalIgnoreCase))
        {
            var daysFromMonday = ((int)localToday.DayOfWeek + 6) % 7;
            var monday = localToday.AddDays(-daysFromMonday);
            return ToUtcHalfOpen(monday, monday.AddDays(7));
        }

        if (string.Equals(range, "thisMonth", StringComparison.OrdinalIgnoreCase))
        {
            var first = new DateOnly(localToday.Year, localToday.Month, 1);
            return ToUtcHalfOpen(first, first.AddMonths(1));
        }

        return null;
    }

    private static (DateTime FromUtc, DateTime ToUtcExclusive) ToUtcHalfOpen(DateOnly fromLocal, DateOnly toLocalExclusive)
    {
        var fromLocalDt = DateTime.SpecifyKind(fromLocal.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var toLocalDt = DateTime.SpecifyKind(toLocalExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return (
            TimeZoneInfo.ConvertTimeToUtc(fromLocalDt, VietnamTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(toLocalDt, VietnamTimeZone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
