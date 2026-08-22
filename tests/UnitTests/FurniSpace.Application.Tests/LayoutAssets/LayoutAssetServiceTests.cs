#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.LayoutAssets;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Services.LayoutAssets;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.LayoutAssets;

public sealed class LayoutAssetServiceTests
{
    static LayoutAssetServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesActiveLayoutAsset()
    {
        var repository = new FakeLayoutAssetRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            new CreateLayoutAssetRequestDto
            {
                AssetCode = " stair-001 ",
                AssetName = " Straight Stair ",
                AssetType = LayoutAssetType.STAIR,
                Description = " Main stair "
            },
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("STAIR-001", result.Data.AssetCode);
        Assert.Equal("Straight Stair", result.Data.AssetName);
        Assert.Equal(LayoutAssetStatus.ACTIVE, result.Data.Status);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCode_ReturnsConflict()
    {
        var repository = new FakeLayoutAssetRepository { CodeExists = true };
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            new CreateLayoutAssetRequestDto
            {
                AssetCode = "STAIR-001",
                AssetName = "Straight Stair",
                AssetType = LayoutAssetType.STAIR
            },
            Guid.NewGuid());

        Assert.Equal(409, result.Status);
        Assert.Equal(LayoutAssetErrorCodes.CodeDuplicate, result.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInvalidTransition_ReturnsBadRequest()
    {
        var assetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var repository = new FakeLayoutAssetRepository
        {
            Detail = new LayoutAsset
            {
                LayoutAssetId = assetId,
                AssetCode = "STAIR-001",
                AssetName = "Straight Stair",
                AssetType = LayoutAssetType.STAIR,
                Status = LayoutAssetStatus.ARCHIVED,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var service = CreateService(repository);

        var result = await service.UpdateStatusAsync(
            assetId,
            new UpdateLayoutAssetStatusRequestDto { Status = LayoutAssetStatus.INACTIVE });

        Assert.Equal(400, result.Status);
        Assert.Equal(LayoutAssetErrorCodes.InvalidStatusTransition, result.ErrorCode);
    }

    [Fact]
    public async Task GetRoomPlannerCatalogAsync_ForDesigner_ForcesActiveStatus()
    {
        var repository = new FakeLayoutAssetRepository();
        var service = CreateService(repository);

        var result = await service.GetRoomPlannerCatalogAsync(
            new RoomPlannerLayoutAssetCatalogQueryDto
            {
                Page = 1,
                PageSize = 20
            },
            "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(LayoutAssetStatus.ACTIVE, repository.Status);
    }

    [Fact]
    public async Task GetRoomPlannerCatalogAsync_ForDesignerWithInactiveFilter_ReturnsForbidden()
    {
        var repository = new FakeLayoutAssetRepository();
        var service = CreateService(repository);

        var result = await service.GetRoomPlannerCatalogAsync(
            new RoomPlannerLayoutAssetCatalogQueryDto
            {
                Status = LayoutAssetStatus.INACTIVE,
                Page = 1,
                PageSize = 20
            },
            "DESIGNER");

        Assert.Equal(403, result.Status);
        Assert.Equal(LayoutAssetErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesMetadata()
    {
        var assetId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var repository = new FakeLayoutAssetRepository
        {
            Detail = CreateAsset(assetId)
        };
        var service = CreateService(repository);

        var result = await service.UpdateAsync(
            assetId,
            new UpdateLayoutAssetRequestDto
            {
                AssetName = " Updated Stair ",
                AssetType = LayoutAssetType.STAIR,
                Description = " Updated description "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Updated Stair", result.Data!.AssetName);
        Assert.Equal("Updated description", result.Data.Description);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidFileType_ReturnsBadRequest()
    {
        var assetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var repository = new FakeLayoutAssetRepository
        {
            Detail = CreateAsset(assetId)
        };
        var service = CreateService(repository);

        var result = await service.UploadFileAsync(
            assetId,
            Guid.NewGuid(),
            new UploadCatalogFileRequestDto
            {
                Content = new MemoryStream([1, 2, 3]),
                OriginalFileName = "preview.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 3,
                FileType = FileType.PRODUCT_PREVIEW
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(LayoutAssetErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task SetPrimaryFileAsync_WithValidPreviewFile_SetsPrimaryWithinPreviewGroup()
    {
        var assetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var previewFileId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var repository = new FakeLayoutAssetRepository { Detail = CreateAsset(assetId) };
        var fileRepository = new FakeLayoutAssetFileRepository
        {
            SelectedLink = new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = previewFileId,
                ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                ReferenceId = assetId,
                FileType = FileType.PREVIEW
            },
            ReferenceLinks =
            [
                new FileLink
                {
                    FileLinkId = Guid.NewGuid(),
                    FileId = Guid.NewGuid(),
                    ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                    ReferenceId = assetId,
                    FileType = FileType.PREVIEW,
                    IsPrimary = true
                },
                new FileLink
                {
                    FileLinkId = Guid.NewGuid(),
                    FileId = previewFileId,
                    ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                    ReferenceId = assetId,
                    FileType = FileType.PREVIEW,
                    IsPrimary = false
                }
            ]
        };
        var service = CreateService(repository, fileRepository);

        var result = await service.SetPrimaryFileAsync(assetId, previewFileId);

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.IsPrimary);
        Assert.True(fileRepository.ReferenceLinks.All(link =>
            link.FileType != FileType.PREVIEW || link.IsPrimary == (link.FileId == previewFileId)));
        Assert.Equal(1, fileRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_EnrichesPrimaryFileSummaries()
    {
        var assetId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var modelFileId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var repository = new FakeLayoutAssetRepository { Detail = CreateAsset(assetId) };
        var fileRepository = new FakeLayoutAssetFileRepository
        {
            CatalogFiles =
            [
                new CatalogFileReadModel
                {
                    FileId = modelFileId,
                    ReferenceId = assetId,
                    ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                    FileType = FileType.MODEL_3D,
                    OriginalFileName = "stair.glb",
                    FileUrl = "https://example.com/stair.glb",
                    MimeType = "model/gltf-binary",
                    Status = FileStatus.ACTIVE,
                    IsPrimary = true,
                    UploadedAt = DateTime.UtcNow
                },
                new CatalogFileReadModel
                {
                    FileId = Guid.NewGuid(),
                    ReferenceId = assetId,
                    ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                    FileType = FileType.PREVIEW,
                    OriginalFileName = "preview.webp",
                    FileUrl = "https://example.com/preview.webp",
                    MimeType = "image/webp",
                    Status = FileStatus.ACTIVE,
                    IsPrimary = true,
                    UploadedAt = DateTime.UtcNow
                }
            ]
        };
        var service = CreateService(repository, fileRepository);

        var result = await service.GetByIdAsync(assetId, "ADMIN");

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data!.PrimaryModel);
        Assert.Equal(modelFileId, result.Data.PrimaryModel.FileId);
        Assert.NotNull(result.Data.PrimaryPreview);
        Assert.Equal(2, result.Data.Files.Count);
    }

    private static LayoutAsset CreateAsset(Guid assetId)
    {
        return new LayoutAsset
        {
            LayoutAssetId = assetId,
            AssetCode = "STAIR-001",
            AssetName = "Straight Stair",
            AssetType = LayoutAssetType.STAIR,
            Status = LayoutAssetStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static LayoutAssetService CreateService(
        FakeLayoutAssetRepository repository,
        FakeLayoutAssetFileRepository? fileRepository = null)
    {
        var files = fileRepository ?? new FakeLayoutAssetFileRepository();
        return new LayoutAssetService(
            repository,
            files,
            TestUnitOfWork.ForSaveChanges(_ =>
            {
                repository.SaveChangesCallCount++;
                files.SaveChangesCallCount++;
                return Task.FromResult(1);
            }),
            new LayoutAssetServiceDependencies(
                new FakeFileStorageService(),
                new FileUploadSettings
                {
                    MaxFileSizeBytes = 1024 * 1024,
                    AllowedExtensions = [".jpg", ".jpeg", ".webp", ".glb"],
                    AllowedMimeTypes = ["image/jpeg", "image/webp", "model/gltf-binary"]
                },
                new FirebaseStorageSettings
                {
                    Bucket = "test-bucket",
                    LayoutAssetFilesPrefix = "layout-assets"
                }));
    }

    private sealed class FakeLayoutAssetRepository : ILayoutAssetRepository
    {
        public LayoutAsset? Detail { get; set; }
        public bool CodeExists { get; set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; set; }
        public LayoutAssetStatus? Status { get; private set; }

        public Task AddAsync(LayoutAsset layoutAsset, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            Detail = layoutAsset;
            return Task.CompletedTask;
        }

        public Task<LayoutAsset?> GetByIdAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Detail?.LayoutAssetId == layoutAssetId ? Detail : null);
        }

        public Task<LayoutAsset?> GetForUpdateAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
        {
            return GetByIdAsync(layoutAssetId, cancellationToken);
        }

        public Task<bool> AssetCodeExistsAsync(string normalizedAssetCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeExists);
        }

        public Task<bool> AssetCodeExistsExceptAsync(
            string normalizedAssetCode,
            Guid layoutAssetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeExists);
        }

        public Task<IReadOnlyList<LayoutAsset>> GetPagedAsync(
            LayoutAssetType? assetType,
            LayoutAssetStatus? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            return Task.FromResult<IReadOnlyList<LayoutAsset>>(Detail is null ? [] : [Detail]);
        }

        public Task<int> CountAsync(
            LayoutAssetType? assetType,
            LayoutAssetStatus? status,
            string? search,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            return Task.FromResult(Detail is null ? 0 : 1);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeLayoutAssetFileRepository : IProjectFileRepository
    {
        public IReadOnlyList<CatalogFileReadModel> CatalogFiles { get; set; } = [];
        public FileLink? SelectedLink { get; set; }
        public List<FileLink> ReferenceLinks { get; set; } = [];
        public int SaveChangesCallCount { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredFile>>([]);

        public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();

        public void Update(StoredFile entity)
        {
        }

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(StoredFile entity)
        {
        }

        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
        {
        }

        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<StoredFile?>(null);
        }

        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CatalogFiles);
        }

        public Task<FileLink?> GetFileLinkEntityAsync(
            string referenceType,
            Guid referenceId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SelectedLink?.FileId == fileId ? SelectedLink : null);
        }

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByReferenceAsync(
            string referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FileLink>>(ReferenceLinks);
        }

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FileLink>>([]);
        }

        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);

        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
            string referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<FileMetadataReadModel?>(null);

        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
            FileReferenceQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new FileReferencePageReadModel());

        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default)
            => Task.FromResult<FileLinkReadModel?>(null);

        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);

        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
            Guid productId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductPreviewImageReadModel?>(null);

        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);

        public Task<int> CountProductVersionPreviewFilesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);

        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
            Guid projectId,
            string query,
            int page,
            int limit,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);

        public Task<int> CountSearchByProjectAsync(
            Guid projectId,
            string query,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<bool> HasProjectFileWithTypesAsync(
            Guid projectId,
            IReadOnlyCollection<FileType> fileTypes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ProjectLinkedFileReadModel?> GetProjectLinkedActiveFileAsync(
            Guid projectId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectLinkedFileReadModel?>(null);
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://example.com/{request.ObjectName}"
            });
        }

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
