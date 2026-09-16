using FurniSpace.Infrastructure.ReadModels.Dashboard;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IDashboardQueueReadRepository
{
    Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetSalesQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<SalesDashboardKpisReadModel> GetSalesKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesUnpaidRemainingRowReadModel>> GetSalesUnpaidRemainingRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesOverdueTaskRowReadModel>> GetSalesOverdueTaskRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetDesignerQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<DesignerDashboardKpisReadModel> GetDesignerKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DesignerConfirmedMeasurementRowReadModel>> GetDesignerConfirmedMeasurementRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DesignerProposalConsultingRowReadModel>> GetDesignerProposalConsultingRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DesignerRevisionRequestedRowReadModel>> GetDesignerRevisionRequestedRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DesignerAssignedProjectRowReadModel>> GetDesignerAssignedProjectRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardProductionQueueRowReadModel>> GetProductionQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardProductionCustomizationQueueRowReadModel>> GetProductionCustomizationQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardProductionDeliveryQueueRowReadModel>> GetProductionDeliveryQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<List<ProjectPhaseDeadlineRiskRowReadModel>> GetProjectPhaseDeadlineRiskRowsAsync(
        ProjectPhaseDeadlineRiskQueryReadModel query,
        CancellationToken cancellationToken = default);
}
