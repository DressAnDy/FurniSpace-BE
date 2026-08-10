using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;

namespace FurniSpace.Application.Interfaces.Financial;

public interface IAdminFinancialService
{
    Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
        AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialReceivablesDto>> GetReceivablesAsync(
        AdminFinancialReceivablesQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialPaymentBreakdownDto>> GetPaymentBreakdownAsync(
        AdminFinancialPaymentBreakdownQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialCollectionTrendDto>> GetCollectionTrendAsync(
        AdminFinancialCollectionTrendQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialProjectsDto>> GetProjectsAsync(
        AdminFinancialProjectsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialProjectRowDto>> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialPaymentsDto>> GetPaymentsAsync(
        AdminFinancialPaymentsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialExceptionsDto>> GetExceptionsAsync(
        AdminFinancialExceptionsQueryDto query,
        CancellationToken cancellationToken = default);
}
