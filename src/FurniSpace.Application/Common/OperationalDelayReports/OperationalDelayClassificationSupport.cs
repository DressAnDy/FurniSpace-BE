using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.OperationalDelayReports;

internal static class OperationalDelayClassificationSupport
{
    internal static OperationalDelayState DeriveDelayState(DateOnly deadlineSnapshot, DateTime reportedAtUtc)
    {
        var reportDate = DateOnly.FromDateTime(reportedAtUtc);
        return reportDate <= deadlineSnapshot
            ? OperationalDelayState.AT_RISK
            : OperationalDelayState.OVERDUE;
    }
}
