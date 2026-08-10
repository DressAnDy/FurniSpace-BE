namespace FurniSpace.Application.Common.Financial;

public sealed record FinancialReportingPeriod(
    string Type,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTime FromUtc,
    DateTime ToUtcExclusive,
    string Timezone);
