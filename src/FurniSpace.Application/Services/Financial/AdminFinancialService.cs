using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Financial;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Financial;

public sealed class AdminFinancialService : IAdminFinancialService
{
    private readonly IFinancialReadRepository _financial;

    public AdminFinancialService(IFinancialReadRepository financial)
    {
        _financial = financial;
    }

    public async Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
        AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialSummaryQueryDto();
        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!string.Equals(currency, FinancialReportingConstants.DefaultCurrency, StringComparison.Ordinal))
        {
            return ServiceResult<AdminFinancialSummaryDto>.Failure(
                Error.BadRequest(
                    AdminFinancialErrorCodes.CurrencyInvalid,
                    "Financial currency is invalid."));
        }

        if (!FinancialReportingPeriodResolver.TryResolve(
                query,
                DateTimeOffset.UtcNow,
                out var period,
                out var errorCode,
                out var errorMessage))
        {
            return ServiceResult<AdminFinancialSummaryDto>.Failure(
                Error.BadRequest(errorCode, errorMessage));
        }

        var summary = await _financial.GetAdminSummaryAsync(
            period.FromUtc,
            period.ToUtcExclusive,
            DateTime.UtcNow,
            currency,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        return ServiceResult<AdminFinancialSummaryDto>.Success(
            new AdminFinancialSummaryDto
            {
                Period = new AdminFinancialPeriodDto
                {
                    Type = period.Type,
                    From = period.From,
                    To = period.To,
                    Timezone = period.Timezone
                },
                Currency = currency,
                CollectedAmount = summary.CollectedAmount,
                OutstandingPaymentAmount = summary.OutstandingPaymentAmount,
                ContractedReceivableAmount = summary.ContractedReceivableAmount,
                OrderCommercialValue = summary.OrderCommercialValue,
                FailedTransactionCount = summary.FailedTransactionCount,
                ActivePaymentCount = summary.ActivePaymentCount
            },
            "Admin financial summary retrieved successfully.");
    }
}
