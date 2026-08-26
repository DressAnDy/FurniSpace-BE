using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectReviews;

namespace FurniSpace.Application.Interfaces.ProjectReviews;

public interface IProjectReviewConsentService
{
    Task<ServiceResult<ProjectReviewPublicConsentDto>> UpdatePublicConsentAsync(
        Guid reviewId,
        Guid currentUserId,
        UpdateProjectReviewPublicConsentRequestDto request,
        CancellationToken cancellationToken = default);
}
