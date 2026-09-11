using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;

namespace FurniSpace.Application.Interfaces.Financial;

public interface IAdminFinancialDiscountService
{
    Task<ServiceResult<AdminFinancialDiscountSummaryDto>> GetSummaryAsync(
        AdminFinancialDiscountSummaryQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialDiscountProjectsDto>> GetProjectsAsync(
        AdminFinancialDiscountProjectsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialDiscountOrderDetailDto>> GetOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialDiscountTrendDto>> GetTrendAsync(
        AdminFinancialDiscountTrendQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialDiscountExceptionsDto>> GetExceptionsAsync(
        AdminFinancialDiscountExceptionsQueryDto query,
        CancellationToken cancellationToken = default);
}
