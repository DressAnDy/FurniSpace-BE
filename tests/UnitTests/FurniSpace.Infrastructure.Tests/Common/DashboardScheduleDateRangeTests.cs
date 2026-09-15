using System;
using FurniSpace.Infrastructure.Common.Dashboard;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Common;

public sealed class DashboardScheduleDateRangeTests
{
    [Fact]
    public void TryResolve_ThisWeek_IncludesMondayThroughSundayInVietnam()
    {
        // Monday 2026-08-17 12:00 UTC = 19:00 VN
        var utcNow = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

        var bounds = DashboardScheduleDateRange.TryResolve("thisWeek", utcNow);

        Assert.NotNull(bounds);
        // VN Mon 00:00 = UTC Sun 17:00 previous day
        Assert.Equal(new DateTime(2026, 8, 16, 17, 0, 0, DateTimeKind.Utc), bounds.Value.FromUtc);
        Assert.Equal(new DateTime(2026, 8, 23, 17, 0, 0, DateTimeKind.Utc), bounds.Value.ToUtcExclusive);

        var inWeek = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(inWeek >= bounds.Value.FromUtc && inWeek < bounds.Value.ToUtcExclusive);
    }
}
