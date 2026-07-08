using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectAreas;

namespace FurniSpace.Application.Interfaces.ProjectAreas;

public interface IProjectAreaService
{
    Task<ServiceResult<ProjectAreaDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ProjectAreaDto>>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        bool includeCancelled,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectAreaDto>> GetDetailAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectAreaDto>> UpdateAsync(
        Guid projectAreaId,
        Guid currentUserId,
        UpdateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectAreaDto>> CancelAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
