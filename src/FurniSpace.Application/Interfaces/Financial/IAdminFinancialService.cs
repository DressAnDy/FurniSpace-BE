using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;

namespace FurniSpace.Application.Interfaces.Financial;

public interface IAdminFinancialService
{
    Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
        AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default);
}
