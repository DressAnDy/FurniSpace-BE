using FurniSpace.Application.Common;
using FurniSpace.Shared.DTOs.Reports;

namespace FurniSpace.Application.Interfaces.Reports;

public interface IAdminReportService
{
    Task<ServiceResult<ReportOverviewDto>> GetOverviewAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessReportDto>> GetBusinessAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectReportDto>> GetProjectsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommercialReportDto>> GetCommercialAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionReportDto>> GetProductionAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeliveryReportDto>> GetDeliveryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CatalogReportDto>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ProjectAgingItemDto>>> GetProjectAgingAsync(
        ProjectAgingQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommercialTrendDto>> GetCommercialTrendAsync(
        CommercialTrendQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CatalogBestsellersDto>> GetCatalogBestsellersAsync(
        CatalogBestsellersQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeliveryReviewsDto>> GetDeliveryReviewsAsync(
        DeliveryReviewsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ProductionWorkloadItemDto>>> GetProductionWorkloadAsync(
        ProductionWorkloadQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionWorkloadSummaryDto>> GetProductionWorkloadSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ReportExportFileDto>> ExportAsync(
        ReportExportQueryDto query,
        CancellationToken cancellationToken = default);
}
