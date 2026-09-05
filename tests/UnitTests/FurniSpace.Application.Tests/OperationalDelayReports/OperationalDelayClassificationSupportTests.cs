using System;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.OperationalDelayReports;

public sealed class OperationalDelayClassificationSupportTests
{
    [Fact]
    public void DeriveDelayState_WhenReportedOnOrBeforeDeadline_ReturnsAtRisk()
    {
        var deadline = new DateOnly(2026, 9, 10);
        var reportedAt = new DateTime(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc);

        var state = OperationalDelayClassificationSupport.DeriveDelayState(deadline, reportedAt);

        Assert.Equal(OperationalDelayState.AT_RISK, state);
    }

    [Fact]
    public void DeriveDelayState_WhenReportedAfterDeadline_ReturnsOverdue()
    {
        var deadline = new DateOnly(2026, 9, 10);
        var reportedAt = new DateTime(2026, 9, 11, 0, 0, 1, DateTimeKind.Utc);

        var state = OperationalDelayClassificationSupport.DeriveDelayState(deadline, reportedAt);

        Assert.Equal(OperationalDelayState.OVERDUE, state);
    }
}
