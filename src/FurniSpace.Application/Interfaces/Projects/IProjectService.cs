using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;

namespace FurniSpace.Application.Interfaces.Projects;

public interface IProjectService
{
    Task<ServiceResult<ProjectDto>> CreateAsync(
        Guid currentUserId,
        CreateProjectRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectDto>> GetByIdAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectSalesAssignmentDto>> AssignSalesAsync(
        Guid projectId,
        Guid currentUserId,
        AssignProjectSalesRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectInformationRequestDto>> RequestInformationAsync(
        Guid projectId,
        Guid currentUserId,
        RequestProjectInformationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectBasicInformationDto>> UpdateBasicInformationAsync(
        Guid projectId,
        Guid currentUserId,
        UpdateProjectBasicInformationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectStatusUpdateDto>> UpdateStatusAsync(
        Guid projectId,
        Guid currentUserId,
        UpdateProjectStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectRejectionDto>> RejectAsync(
        Guid projectId,
        Guid currentUserId,
        RejectProjectRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectDesignerAssignmentDto>> AssignDesignerAsync(
        Guid projectId,
        Guid currentUserId,
        AssignProjectDesignerRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectListResponseDto>> GetListAsync(
        Guid currentUserId,
        ProjectListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectsByUserResponseDto>> GetByUserAsync(
        Guid userId,
        Guid currentUserId,
        GetProjectsByUserQueryDto query,
        CancellationToken cancellationToken = default);
}
