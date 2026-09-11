using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectFileRepository : IGenericRepository<StoredFile>
{
    Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task AddFileLinkAsync(
        FileLink fileLink,
        CancellationToken cancellationToken = default);

    Task<FileMetadataReadModel?> GetFileMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
        FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<FileLinkReadModel?> GetFileLinkAsync(
        Guid fileLinkId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<FileLink?> GetFileLinkEntityAsync(
        string referenceType,
        Guid referenceId,
        Guid fileId,
        CancellationToken cancellationToken = default) => Task.FromResult<FileLink?>(null);

    Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);

    void RemoveFileLinks(IEnumerable<FileLink> fileLinks);

    Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default);

    Task<int> CountProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<int> CountProductVersionPreviewFilesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default);

    Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default);

    Task<bool> HasProjectFileWithTypesAsync(
        Guid projectId,
        IReadOnlyCollection<FileType> fileTypes,
        CancellationToken cancellationToken = default);

    Task<ProjectLinkedFileReadModel?> GetProjectLinkedActiveFileAsync(
        Guid projectId,
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectLinkedFileReadModel?>(null);

    Task<bool> ExistsByStoragePathAsync(
        string storagePath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    Task<MeasurementImageGalleryPageReadModel> GetMeasurementImageGalleryAsync(
        MeasurementImageGalleryQueryReadModel query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MeasurementImageGalleryPageReadModel());

    Task<bool> HasMeasurementScheduleLinkInProjectAsync(
        Guid fileId,
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
