using FurniSpace.Shared.DTOs.Reports;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IAdminReportRepository
{
    Task<IReadOnlyList<(string Key, long Count)>> CountAccountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string Key, long Count, string? Label)>> CountAccountsByRoleAsync(CancellationToken cancellationToken = default);

    Task<ProjectReportDto> GetProjectReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<CommercialReportDto> GetCommercialReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<ProductionReportDto> GetProductionReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<DeliveryReportDto> GetDeliveryReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<CatalogReportDto> GetCatalogReportAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProjectAgingItemDto> Items, int Total)> GetProjectAgingAsync(
        int thresholdDays,
        string? bucket,
        string? reason,
        int page,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken = default);

    Task<CommercialTrendDto> GetCommercialTrendAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default);

    Task<CatalogBestsellersDto> GetCatalogBestsellersAsync(
        DateTime from,
        DateTime to,
        string metric,
        int limit,
        CancellationToken cancellationToken = default);

    Task<DeliveryReviewsDto> GetDeliveryReviewsAsync(
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProductionWorkloadItemDto> Items, int Total, ProductionWorkloadSummaryDto Summary)> GetProductionWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveRequests,
        string? search,
        string? capacityState,
        string sortBy,
        CancellationToken cancellationToken = default);
}
