using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Interfaces.OperationalDelayReports;

public interface IOperationalDelayReportService
{
    Task<ServiceResult<OperationalDelayReportDto>> CreateProductionReportAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProductionDelayReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalDelayReportDto>> CreateDeliveryReportAsync(
        Guid projectId,
        Guid currentUserId,
        CreateDeliveryDelayReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalDelayReportListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        OperationalDelayPhase phase,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalDelayReportDto>> GetDetailAsync(
        Guid reportId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
