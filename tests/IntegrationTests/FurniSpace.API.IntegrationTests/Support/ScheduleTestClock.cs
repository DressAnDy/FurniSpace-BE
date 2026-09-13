namespace FurniSpace.API.IntegrationTests.Support;

/// <summary>
/// Builds UTC instants that map to fixed Vietnam local clock times,
/// so MEASUREMENT/DELIVERY create calls stay inside 06:00-22:00 VN business hours.
/// </summary>
public static class ScheduleTestClock
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime VietnamLocalAsUtc(int dayOffset = 1, int hour = 8, int minute = 0)
    {
        var day = DateTime.UtcNow.AddHours(7).Date.AddDays(dayOffset);
        var local = new DateTime(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(local - VietnamOffset, DateTimeKind.Utc);
    }
}
