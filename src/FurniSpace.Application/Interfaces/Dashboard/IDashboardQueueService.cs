using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Dashboard;

namespace FurniSpace.Application.Interfaces.Dashboard;

public interface IDashboardQueueService
{
    Task<ServiceResult<DashboardQueueResponseDto>> GetSalesActionQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SalesDashboardKpisDto>> GetSalesKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DashboardQueueResponseDto>> GetDesignerWorkQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DesignerDashboardKpisDto>> GetDesignerKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DashboardQueueResponseDto>> GetProductionQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionDashboardKpisDto>> GetProductionKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectPhaseDeadlineRiskResponseDto>> GetProjectPhaseDeadlineRisksAsync(
        Guid currentUserId,
        ProjectPhaseDeadlineRiskQueryDto query,
        CancellationToken cancellationToken = default);
}
