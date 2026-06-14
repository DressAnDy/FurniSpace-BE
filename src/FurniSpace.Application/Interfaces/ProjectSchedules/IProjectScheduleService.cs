using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectSchedules;

namespace FurniSpace.Application.Interfaces.ProjectSchedules;

public interface IProjectScheduleService
{
    Task<ServiceResult<ProjectScheduleDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectScheduleListResponseDto>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectScheduleListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectScheduleDto>> GetDetailAsync(
        Guid scheduleId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectScheduleDto>> UpdateAsync(
        Guid scheduleId,
        Guid currentUserId,
        UpdateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectScheduleDto>> UpdateStatusAsync(
        Guid scheduleId,
        Guid currentUserId,
        UpdateProjectScheduleStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectScheduleListResponseDto>> GetMyAssignedAsync(
        Guid currentUserId,
        ProjectScheduleListQueryDto query,
        CancellationToken cancellationToken = default);
}
