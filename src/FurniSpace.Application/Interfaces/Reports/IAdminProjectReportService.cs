#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Reports;

namespace FurniSpace.Application.Interfaces.Reports;

public interface IAdminProjectReportService
{
    Task<ServiceResult<PagedResult<AdminProjectReportListItemDto>>> GetListAsync(
        AdminProjectReportsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminProjectReportDetailDto>> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
