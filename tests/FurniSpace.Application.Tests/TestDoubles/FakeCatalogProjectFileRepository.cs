#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Tests.TestDoubles;

[SuppressMessage(
    "Minor Code Smell",
    "S2325",
    Justification = "IProjectFileRepository test double; interface members cannot be static.")]
[SuppressMessage(
    "Minor Code Smell",
    "S1172",
    Justification = "Stub methods must keep interface parameter lists.")]
public sealed class FakeCatalogProjectFileRepository : IProjectFileRepository
{
    public IReadOnlyList<CatalogFileReadModel> CatalogFiles { get; init; } = [];
    public List<StoredFile> StoredFiles { get; } = [];
    public List<FileLink> FileLinks { get; } = [];

    public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default)
    {
        _ = customerVisibleOnly;
        _ = cancellationToken;
        var normalizedReferenceType = referenceType.Trim().ToUpperInvariant();
        var idSet = referenceIds.ToHashSet();
        var files = CatalogFiles
            .Where(file =>
                string.Equals(file.ReferenceType, normalizedReferenceType, StringComparison.Ordinal) &&
                idSet.Contains(file.ReferenceId))
            .ToList();

        return Task.FromResult<IReadOnlyList<CatalogFileReadModel>>(files);
    }

    public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullProjectAccess(projectId, cancellationToken);

    public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullReferenceProjectAccess(
            referenceType,
            referenceId,
            cancellationToken);

    public Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullRoleName(accountId, cancellationToken);

    public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
    {
        FileLinks.Add(fileLink);
        return Task.CompletedTask;
    }

    public Task<FileMetadataReadModel?> GetFileMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullFileMetadata(fileId, cancellationToken);

    public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
        FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.EmptyFileReferencePage(query, cancellationToken);

    public Task<FileLinkReadModel?> GetFileLinkAsync(
        Guid fileLinkId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullFileLink(fileLinkId, cancellationToken);

    public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.EmptyFileLinks(fileId, cancellationToken);

    public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
    {
        _ = fileLinks;
    }

    public IQueryable<StoredFile> Query() => StoredFiles.AsQueryable();

    public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullStoredFile(id, cancellationToken);

    public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StoredFile>>(StoredFiles);

    public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
    {
        StoredFiles.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
    {
        _ = entities;
        return Task.CompletedTask;
    }

    public void Update(StoredFile entity)
    {
        _ = entity;
    }

    public void Remove(StoredFile entity)
    {
        _ = entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.SaveChanges(cancellationToken);

    public Task<int> CountProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.ZeroCount(productId, cancellationToken);

    public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.EmptyProductPreviewFiles(productId, cancellationToken);

    public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.NullProductPreviewFile(productId, fileId, cancellationToken);

    public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.EmptyFileLinks(productId, cancellationToken);

    public Task<int> CountProductVersionPreviewFilesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.ZeroCount(productVersionId, cancellationToken);

    public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default) =>
        ProjectFileRepositoryStubResponses.EmptyFileLinks(productVersionId, cancellationToken);
}

[SuppressMessage(
    "Minor Code Smell",
    "S2325",
    Justification = "Static helpers for IProjectFileRepository test stubs.")]
[SuppressMessage(
    "Minor Code Smell",
    "S1172",
    Justification = "Stub helpers mirror interface parameter lists.")]
internal static class ProjectFileRepositoryStubResponses
{
    public static Task<ProjectFileAccessReadModel?> NullProjectAccess(
        Guid _,
        CancellationToken __) =>
        Task.FromResult<ProjectFileAccessReadModel?>(null);

    public static Task<ProjectFileAccessReadModel?> NullReferenceProjectAccess(
        string _,
        Guid __,
        CancellationToken ___) =>
        Task.FromResult<ProjectFileAccessReadModel?>(null);

    public static Task<string?> NullRoleName(Guid _, CancellationToken __) =>
        Task.FromResult<string?>(null);

    public static Task<FileMetadataReadModel?> NullFileMetadata(Guid _, CancellationToken __) =>
        Task.FromResult<FileMetadataReadModel?>(null);

    public static Task<FileReferencePageReadModel> EmptyFileReferencePage(
        FileReferenceQueryReadModel _,
        CancellationToken __) =>
        Task.FromResult(new FileReferencePageReadModel());

    public static Task<FileLinkReadModel?> NullFileLink(Guid _, CancellationToken __) =>
        Task.FromResult<FileLinkReadModel?>(null);

    public static Task<IReadOnlyList<FileLink>> EmptyFileLinks(Guid _, CancellationToken __) =>
        Task.FromResult<IReadOnlyList<FileLink>>([]);

    public static Task<StoredFile?> NullStoredFile(Guid _, CancellationToken __) =>
        Task.FromResult<StoredFile?>(null);

    public static Task<int> SaveChanges(CancellationToken _) =>
        Task.FromResult(1);

    public static Task<int> ZeroCount(Guid _, CancellationToken __) =>
        Task.FromResult(0);

    public static Task<IReadOnlyList<ProductPreviewImageReadModel>> EmptyProductPreviewFiles(
        Guid _,
        CancellationToken __) =>
        Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);

    public static Task<ProductPreviewImageReadModel?> NullProductPreviewFile(
        Guid _,
        Guid __,
        CancellationToken ___) =>
        Task.FromResult<ProductPreviewImageReadModel?>(null);

    public static Task<bool> FalseHasProjectFileWithTypes(
        Guid _,
        IReadOnlyCollection<FileType> __,
        CancellationToken ___) =>
        Task.FromResult(false);
}
