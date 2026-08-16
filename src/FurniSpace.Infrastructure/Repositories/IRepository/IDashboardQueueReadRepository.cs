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

    Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetDesignerQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<DesignerDashboardKpisReadModel> GetDesignerKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardProductionQueueRowReadModel>> GetProductionQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);

    Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default);
}
