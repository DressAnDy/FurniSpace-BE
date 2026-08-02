using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.CustomizationRequests;

namespace FurniSpace.Application.Interfaces.CustomizationRequests;

public interface ICustomizationRequestService
{
    Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CustomizationRequestQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> GetDetailAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestVersionListResponseDto>> GetVersionsAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestVersionDto>> GetVersionDetailAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
        Guid proposalItemId,
        Guid currentUserId,
        SubmitCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>> CreateVersionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CreateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestVersionDto>> UpdateDraftVersionAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        UpdateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestVersionDto>> SubmitVersionForReviewAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestVersionDto>> WithdrawVersionAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> AcceptVersionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        AcceptCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> CancelAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancelCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionCustomizationVersionListResponseDto>> GetProductionVersionQueueAsync(
        Guid currentUserId,
        ProductionCustomizationVersionQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionCustomizationVersionDetailDto>> GetProductionVersionDetailAsync(
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionCustomizationVersionDetailDto>> ReviewVersionAsync(
        Guid customizationRequestVersionId,
        Guid currentUserId,
        ReviewCustomizationVersionDto request,
        CancellationToken cancellationToken = default);
}
