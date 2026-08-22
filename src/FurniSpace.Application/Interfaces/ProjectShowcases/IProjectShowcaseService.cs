using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectShowcases;

namespace FurniSpace.Application.Interfaces.ProjectShowcases;

public interface IProjectShowcaseService
{
    Task<ServiceResult<ProjectShowcaseDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectShowcaseRequestDto? request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> UpdateAsync(
        Guid showcaseId,
        Guid currentUserId,
        UpdateProjectShowcaseRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> SubmitAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> PublishAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> ArchiveAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseMediaDto>> AddMediaAsync(
        Guid showcaseId,
        Guid currentUserId,
        AddProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> ReorderMediaAsync(
        Guid showcaseId,
        Guid currentUserId,
        ReorderProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseMediaDto>> SetCoverAsync(
        Guid showcaseId,
        Guid mediaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectShowcaseDto>> RemoveMediaAsync(
        Guid showcaseId,
        Guid mediaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PublicShowcaseListResponseDto>> GetPublicListAsync(
        PublicShowcaseQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PublicShowcaseDetailDto>> GetPublicBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
