using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectFiles;

namespace FurniSpace.Application.Interfaces.ProjectFiles;

public interface IProjectFileService
{
    Task<ServiceResult<PrepareProjectFileUploadResponseDto>> PrepareProjectFileUploadAsync(
        Guid projectId,
        Guid currentUserId,
        PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectFileUploadAsync(
        Guid projectId,
        Guid currentUserId,
        CompleteProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PrepareProjectAreaFileUploadResponseDto>> PrepareProjectAreaFileUploadAsync(
        Guid projectAreaId,
        Guid currentUserId,
        PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectAreaFileUploadAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CompleteProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FileDetailResponseDto>> GetFileDetailAsync(
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectFilesResponseDto>> GetProjectFilesAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectFilesResponseDto>> GetProjectAreaFilesAsync(
        Guid projectAreaId,
        Guid currentUserId,
        ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectAreaFilePrimaryResponseDto>> SetProjectAreaPrimaryFileAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectFileSearchResponseDto>> SearchProjectFilesAsync(
        Guid projectId,
        Guid currentUserId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FilesByReferenceResponseDto>> GetFilesByReferenceAsync(
        Guid currentUserId,
        FilesByReferenceQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeleteFileResponseDto>> DeleteFileAsync(
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArchiveFileResponseDto>> ArchiveFileAsync(
        Guid fileId,
        Guid currentUserId,
        ArchiveFileRequestDto request,
        CancellationToken cancellationToken = default);
}
