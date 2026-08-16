using static FurniSpace.Application.Constants.Dashboard.DashboardQueueConstants;

namespace FurniSpace.Application.Common.Dashboard;

public static class DashboardDueHelper
{
    public static DateTime? ToDueAtUtc(DateOnly? date)
    {
        if (!date.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
    }

    public static string? ResolveDueBucket(DateTime? dueAtUtc, DateTime utcNow)
    {
        if (!dueAtUtc.HasValue)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(utcNow);
        var dueDate = DateOnly.FromDateTime(dueAtUtc.Value);
        if (dueDate < today)
        {
            return DueBucketOverdue;
        }

        if (dueDate == today)
        {
            return DueBucketToday;
        }

        var endOfWeek = today.AddDays(7 - (int)today.DayOfWeek);
        if (dueDate <= endOfWeek)
        {
            return DueBucketThisWeek;
        }

        return DueBucketLater;
    }

    public static bool MatchesDateRange(DateTime? dueAtUtc, string? dateRange, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return true;
        }

        if (!dueAtUtc.HasValue)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(utcNow);
        var dueDate = DateOnly.FromDateTime(dueAtUtc.Value);
        var normalized = dateRange.Trim();

        if (string.Equals(normalized, DateRangeToday, StringComparison.OrdinalIgnoreCase))
        {
            return dueDate == today;
        }

        if (string.Equals(normalized, DateRangeThisWeek, StringComparison.OrdinalIgnoreCase))
        {
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(6);
            return dueDate >= startOfWeek && dueDate <= endOfWeek;
        }

        if (string.Equals(normalized, DateRangeThisMonth, StringComparison.OrdinalIgnoreCase))
        {
            return dueDate.Year == today.Year && dueDate.Month == today.Month;
        }

        return true;
    }
}
