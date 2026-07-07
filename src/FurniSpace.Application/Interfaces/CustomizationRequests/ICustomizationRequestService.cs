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

    Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
        Guid proposalItemId,
        Guid currentUserId,
        SubmitCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> DesignerReviewAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        DesignerReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> ProductionReviewAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        ProductionReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> CustomerDecisionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CustomerDecisionCustomizationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomizationRequestDetailDto>> CancelAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancelCustomizationRequestDto request,
        CancellationToken cancellationToken = default);
}
