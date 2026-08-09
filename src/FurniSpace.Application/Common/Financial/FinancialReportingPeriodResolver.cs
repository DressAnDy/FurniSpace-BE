using FurniSpace.Application.DTOs.Financial;

namespace FurniSpace.Application.Common.Financial;

public static class FinancialReportingPeriodResolver
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static bool TryResolve(
        AdminFinancialSummaryQueryDto query,
        DateTimeOffset utcNow,
        out FinancialReportingPeriod period,
        out string errorCode,
        out string errorMessage)
    {
        var periodType = NormalizePeriod(query.Period);
        if (!IsSupportedPeriod(periodType))
        {
            return Fail(
                AdminFinancialErrorCodes.PeriodInvalid,
                "Financial period is invalid.",
                out period,
                out errorCode,
                out errorMessage);
        }

        return periodType switch
        {
            FinancialReportingConstants.PeriodCustom => ResolveCustom(query, out period, out errorCode, out errorMessage),
            FinancialReportingConstants.PeriodThisYear => ResolveCurrentPeriod(periodType, utcNow, year: true, out period, out errorCode, out errorMessage),
            _ => ResolveCurrentPeriod(periodType, utcNow, year: false, out period, out errorCode, out errorMessage)
        };
    }

    public static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? FinancialReportingConstants.DefaultCurrency
            : currency.Trim().ToUpperInvariant();
    }

    private static bool ResolveCurrentPeriod(
        string periodType,
        DateTimeOffset utcNow,
        bool year,
        out FinancialReportingPeriod period,
        out string errorCode,
        out string errorMessage)
    {
        var localNow = utcNow.ToOffset(VietnamOffset);
        var from = year
            ? new DateTimeOffset(localNow.Year, 1, 1, 0, 0, 0, VietnamOffset)
            : new DateTimeOffset(localNow.Year, localNow.Month, 1, 0, 0, 0, VietnamOffset);
        var toExclusive = year ? from.AddYears(1) : from.AddMonths(1);
        period = Create(periodType, from, toExclusive);
        errorCode = string.Empty;
        errorMessage = string.Empty;
        return true;
    }

    private static bool ResolveCustom(
        AdminFinancialSummaryQueryDto query,
        out FinancialReportingPeriod period,
        out string errorCode,
        out string errorMessage)
    {
        if (!query.From.HasValue || !query.To.HasValue)
        {
            return Fail(
                AdminFinancialErrorCodes.DateRangeInvalid,
                "Custom financial period requires from and to.",
                out period,
                out errorCode,
                out errorMessage);
        }

        var from = query.From.Value.ToOffset(VietnamOffset);
        var to = query.To.Value.ToOffset(VietnamOffset);
        if (from > to)
        {
            return Fail(
                AdminFinancialErrorCodes.DateRangeInvalid,
                "Financial date range is invalid.",
                out period,
                out errorCode,
                out errorMessage);
        }

        var toExclusive = IsStartOfDay(to)
            ? new DateTimeOffset(to.Date.AddDays(1), VietnamOffset)
            : to.AddTicks(1);
        period = Create(FinancialReportingConstants.PeriodCustom, from, toExclusive);
        errorCode = string.Empty;
        errorMessage = string.Empty;
        return true;
    }

    private static FinancialReportingPeriod Create(string periodType, DateTimeOffset from, DateTimeOffset toExclusive)
    {
        return new FinancialReportingPeriod(
            periodType,
            from,
            toExclusive.AddTicks(-1),
            from.UtcDateTime,
            toExclusive.UtcDateTime,
            FinancialReportingConstants.ReportingTimezone);
    }

    private static string NormalizePeriod(string? period)
    {
        return string.IsNullOrWhiteSpace(period)
            ? FinancialReportingConstants.PeriodThisMonth
            : period.Trim().ToUpperInvariant();
    }

    private static bool IsSupportedPeriod(string periodType)
    {
        return periodType is
            FinancialReportingConstants.PeriodThisMonth or
            FinancialReportingConstants.PeriodThisYear or
            FinancialReportingConstants.PeriodCustom;
    }

    private static bool IsStartOfDay(DateTimeOffset value)
    {
        return value.TimeOfDay == TimeSpan.Zero;
    }

    private static bool Fail(
        string code,
        string message,
        out FinancialReportingPeriod period,
        out string errorCode,
        out string errorMessage)
    {
        period = default!;
        errorCode = code;
        errorMessage = message;
        return false;
    }
}
