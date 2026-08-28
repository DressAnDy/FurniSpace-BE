using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectReviews;

namespace FurniSpace.Application.Interfaces.ProjectReviews;

public interface IProjectReviewService
{
    Task<ServiceResult<ProjectReviewDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectReviewDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectReviewRequestDto request,
        CancellationToken cancellationToken = default);
}
