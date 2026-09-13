using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductIssues;

namespace FurniSpace.Application.Interfaces.ProductIssues;

public interface IDeliveryProductIssueReportService
{
    Task<ServiceResult<ProductIssueReportDto>> CreateAsync(
        Guid orderId,
        Guid currentUserId,
        CreateProductIssueRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>> PrepareEvidenceUploadAsync(
        Guid orderId,
        Guid currentUserId,
        PrepareProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>> CompleteEvidenceUploadAsync(
        Guid orderId,
        Guid currentUserId,
        CompleteProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductIssueReportListResponseDto>> GetByOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductIssueReportListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductIssueReportDto>> GetDetailAsync(
        Guid issueId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductIssueReportDto>> ResolveAsync(
        Guid issueId,
        Guid currentUserId,
        ResolveProductIssueRequestDto request,
        CancellationToken cancellationToken = default);
}
