#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Xunit;

namespace FurniSpace.Application.Tests.ProductVersions;

public sealed class ProductVersionServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesProductVersion()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(productIds: [productId]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(productId, new CreateProductVersionRequestDto
        {
            VersionCode = " PV-COUNTER-001-V1 ",
            VersionName = " Coffee Counter - Standard Wood ",
            VersionType = ProductVersionType.STANDARD,
            Material = " Plywood ",
            Color = " Wood Brown ",
            Width = 2000m,
            Height = 1050m,
            Depth = 650m,
            EstimatedPrice = 25000000m,
            IsDefault = true,
            IsPublic = true,
            IsProjectSpecific = false
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Product version created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(productId, result.Data.ProductId);
        Assert.Equal("PV-COUNTER-001-V1", result.Data.VersionCode);
        Assert.Equal("Coffee Counter - Standard Wood", result.Data.VersionName);
        Assert.Equal(ProductVersionType.STANDARD, result.Data.VersionType);
        Assert.Equal("Plywood", result.Data.Material);
        Assert.Equal("Wood Brown", result.Data.Color);
        Assert.Equal(2000m, result.Data.Width);
        Assert.Equal(1050m, result.Data.Height);
        Assert.Equal(650m, result.Data.Depth);
        Assert.Equal(25000000m, result.Data.EstimatedPrice);
        Assert.True(result.Data.IsDefault);
        Assert.True(result.Data.IsPublic);
        Assert.False(result.Data.IsProjectSpecific);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
        Assert.Empty(result.Data.Files);
        Assert.Null(result.Data.Thumbnail);
        Assert.Equal(1, repository.ProductExistsCallCount);
        Assert.Equal(1, repository.VersionCodeExistsCallCount);
        Assert.Equal(1, repository.SetDefaultCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithOptionalFieldsBlank_UsesDefaultsAndNulls()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(productIds: [productId]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(productId, new CreateProductVersionRequestDto
        {
            VersionCode = "PV-COUNTER-001-V1",
            VersionName = "Coffee Counter",
            Material = " ",
            Color = " "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProductVersionType.STANDARD, result.Data.VersionType);
        Assert.Null(result.Data.Material);
        Assert.Null(result.Data.Color);
        Assert.False(result.Data.IsDefault);
        Assert.True(result.Data.IsPublic);
        Assert.False(result.Data.IsProjectSpecific);
        Assert.Equal(0, repository.SetDefaultCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(Guid.Empty, new CreateProductVersionRequestDto
        {
            VersionCode = "PV",
            VersionName = "Version"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.ProductExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProductVersionRequestDto
        {
            VersionCode = " ",
            VersionName = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Version name is required.", result.Errors!);
        Assert.Contains("Version code is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.ProductExistsCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithTooLongFields_ReturnsValidationErrors()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProductVersionRequestDto
        {
            VersionCode = new string('C', 51),
            VersionName = new string('N', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Version code must not exceed 50 characters.", result.Errors!);
        Assert.Contains("Version name must not exceed 150 characters.", result.Errors!);
    }

    [Fact]
    public async Task CreateAsync_WithMissingProduct_ReturnsNotFound()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProductVersionRequestDto
        {
            VersionCode = "PV",
            VersionName = "Version"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.ProductExistsCallCount);
        Assert.Equal(0, repository.VersionCodeExistsCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateVersionCode_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(
            versions: [new ProductVersion { ProductVersionId = Guid.NewGuid(), ProductId = productId, VersionCode = "PV" }],
            productIds: [productId]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.CreateAsync(productId, new CreateProductVersionRequestDto
        {
            VersionCode = "PV",
            VersionName = "Version"
        });

        Assert.Equal(409, result.Status);
        Assert.Equal("Product version code already exists.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.VersionCodeExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesProductVersion()
    {
        var productVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(
            versions:
            [
                new ProductVersion
                {
                    ProductVersionId = productVersionId,
                    ProductId = productId,
                    VersionCode = "PV",
                    VersionName = "Old",
                    IsDefault = false,
                    Status = null
                }
            ]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(productVersionId, new UpdateProductVersionRequestDto
        {
            VersionName = " Premium Wood ",
            VersionType = ProductVersionType.CUSTOM,
            Material = " Plywood ",
            Color = " Dark Walnut ",
            Width = 2200m,
            Height = 1050m,
            Depth = 700m,
            EstimatedPrice = 30000000m,
            IsDefault = true,
            IsPublic = true,
            IsProjectSpecific = false
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Product version updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(productVersionId, result.Data.ProductVersionId);
        Assert.Equal("Premium Wood", result.Data.VersionName);
        Assert.Equal(ProductVersionType.CUSTOM, result.Data.VersionType);
        Assert.Equal("Plywood", result.Data.Material);
        Assert.Equal("Dark Walnut", result.Data.Color);
        Assert.Equal(30000000m, result.Data.EstimatedPrice);
        Assert.True(result.Data.IsDefault);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SetDefaultCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithFalseDefaults_UpdatesWithoutSetDefault()
    {
        var productVersionId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(
            versions:
            [
                new ProductVersion
                {
                    ProductVersionId = productVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV",
                    VersionName = "Old",
                    IsDefault = true,
                    IsPublic = false,
                    IsProjectSpecific = true,
                    Status = ProductStatus.ACTIVE
                }
            ]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(productVersionId, new UpdateProductVersionRequestDto
        {
            VersionName = "Version",
            IsDefault = false
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsDefault);
        Assert.True(result.Data.IsPublic);
        Assert.False(result.Data.IsProjectSpecific);
        Assert.Equal(0, repository.SetDefaultCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyId_ReturnsBadRequest()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.Empty, new UpdateProductVersionRequestDto
        {
            VersionName = "Version"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Product version id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductVersionRequestDto
        {
            VersionName = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Version name is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongVersionName_ReturnsValidationError()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductVersionRequestDto
        {
            VersionName = new string('N', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Version name must not exceed 150 characters.", result.Errors!);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingProductVersion_ReturnsNotFound()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductVersionRequestDto
        {
            VersionName = "Version"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Product version not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SetDefaultAsync_WithValidVersion_SetsOnlySelectedDefault()
    {
        var productId = Guid.NewGuid();
        var selectedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(
            versions:
            [
                new ProductVersion { ProductVersionId = selectedId, ProductId = productId, VersionCode = "A", VersionName = "A" },
                new ProductVersion { ProductVersionId = otherId, ProductId = productId, VersionCode = "B", VersionName = "B", IsDefault = true }
            ]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.SetDefaultAsync(selectedId);

        Assert.Equal(200, result.Status);
        Assert.Equal("Default product version updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(selectedId, result.Data.ProductVersionId);
        Assert.Equal(productId, result.Data.ProductId);
        Assert.True(result.Data.IsDefault);
        Assert.True(repository.Versions.Single(version => version.ProductVersionId == selectedId).IsDefault);
        Assert.False(repository.Versions.Single(version => version.ProductVersionId == otherId).IsDefault);
        Assert.Equal(1, repository.SetDefaultCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SetDefaultAsync_WithEmptyId_ReturnsBadRequest()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.SetDefaultAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Product version id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task SetDefaultAsync_WithMissingVersion_ReturnsNotFound()
    {
        var repository = new FakeProductVersionRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.SetDefaultAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Product version not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SetDefaultCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithPublicVersion_ReturnsDetailWithFiles()
    {
        var productVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new FakeProductVersionRepository(
            versions:
            [
                new ProductVersion
                {
                    ProductVersionId = productVersionId,
                    ProductId = productId,
                    VersionCode = "PV-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE,
                    IsPublic = true
                }
            ],
            productIds: [productId]);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                new CatalogFileReadModel
                {
                    FileId = Guid.NewGuid(),
                    FileLinkId = Guid.NewGuid(),
                    ReferenceId = productVersionId,
                    ReferenceType = "PRODUCT_VERSION",
                    FileType = FileType.MODEL_3D,
                    OriginalFileName = "lamp.glb",
                    FileUrl = "https://storage.example.com/lamp.glb",
                    MimeType = "model/gltf-binary",
                    FileSizeBytes = 2048,
                    Visibility = FileVisibility.CUSTOMER_VISIBLE,
                    Status = FileStatus.ACTIVE,
                    UploadedAt = DateTime.UtcNow
                }
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductVersionService(repository, files);

        var result = await service.GetByIdAsync(productVersionId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(productVersionId, result.Data.ProductVersionId);
        Assert.Single(result.Data.Files);
        Assert.Equal(FileType.MODEL_3D, result.Data.Files[0].FileType);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingVersion_ReturnsNotFound()
    {
        var service = CatalogServiceTestHelper.CreateProductVersionService(
            new FakeProductVersionRepository(),
            new FakeCatalogProjectFileRepository());

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Product version not found.", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductVersionService(
            new FakeProductVersionRepository(),
            new FakeCatalogProjectFileRepository());

        var result = await service.GetByIdAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Product version id is required.", result.Message);
    }

    [Fact]
    public async Task UploadFileAsync_WithValidModelFile_UploadsAndPersistsFile()
    {
        var adminId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new VersionUploadFileRepository();
        var storage = new VersionUploadFileStorage();
        var versionRepository = new FakeProductVersionRepository(
            versions:
            [
                new ProductVersion
                {
                    ProductVersionId = productVersionId,
                    ProductId = productId,
                    VersionCode = "PV-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ],
            productIds: [productId]);
        var service = CatalogServiceTestHelper.CreateProductVersionService(
            versionRepository,
            repository,
            storage);

        var result = await service.UploadFileAsync(
            productVersionId,
            adminId,
            new UploadCatalogFileRequestDto
            {
                Content = new MemoryStream([1, 2, 3]),
                OriginalFileName = "lamp-white.glb",
                ContentType = "model/gltf-binary",
                FileSizeBytes = 12,
                FileType = FileType.MODEL_3D
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("Product version file uploaded successfully.", result.Message);
        Assert.Equal("PRODUCT_VERSION", result.Data!.ReferenceType);
        Assert.StartsWith($"product-versions/{productVersionId:D}/", storage.UploadRequest!.ObjectName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadFileAsync_WithMissingVersion_ReturnsNotFound()
    {
        var repository = new VersionUploadFileRepository();
        var service = CatalogServiceTestHelper.CreateProductVersionService(
            new FakeProductVersionRepository(),
            repository);

        var result = await service.UploadFileAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UploadCatalogFileRequestDto
            {
                Content = new MemoryStream([1, 2, 3]),
                OriginalFileName = "lamp-white.glb",
                ContentType = "model/gltf-binary",
                FileSizeBytes = 12,
                FileType = FileType.MODEL_3D
            });

        Assert.Equal(404, result.Status);
        Assert.Equal("Product version not found.", result.Message);
    }

    private sealed class VersionUploadFileStorage : IFileStorageService
    {
        public StorageUploadRequest? UploadRequest { get; private set; }

        public Task<StorageUploadResult> UploadAsync(
            StorageUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            UploadRequest = request;
            return Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://storage.example.com/{request.ObjectName}",
                Bucket = "test-bucket"
            });
        }

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class VersionUploadFileRepository : IProjectFileRepository
    {
        public List<StoredFile> StoredFiles { get; } = [];
        public List<FileLink> FileLinks { get; } = [];

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
        {
            StoredFiles.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        {
            FileLinks.Add(fileLink);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);

        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);

        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<FileMetadataReadModel?>(null);

        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(new FileReferencePageReadModel());

        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default)
            => Task.FromResult<FileLinkReadModel?>(null);

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);

        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }

        public IQueryable<StoredFile> Query() => StoredFiles.AsQueryable();

        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFile?>(StoredFiles.FirstOrDefault(file => file.FileId == id));

        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredFile>>(StoredFiles);

        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(StoredFile entity) { }

        public void Remove(StoredFile entity) { }
    }

    private sealed class FakeProductVersionRepository : IProductVersionRepository
    {
        private readonly List<Guid> _productIds;
        private readonly List<ProductVersion> _versions;

        public FakeProductVersionRepository(
            IReadOnlyList<ProductVersion>? versions = null,
            IReadOnlyList<Guid>? productIds = null)
        {
            _versions = versions?.ToList() ?? [];
            _productIds = productIds?.ToList() ?? [];
        }

        public IReadOnlyList<ProductVersion> Versions => _versions;
        public int VersionCodeExistsCallCount { get; private set; }
        public int ProductExistsCallCount { get; private set; }
        public int SetDefaultCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }

        public Task<bool> VersionCodeExistsAsync(
            string versionCode,
            CancellationToken cancellationToken = default)
        {
            VersionCodeExistsCallCount++;
            return Task.FromResult(_versions.Any(version =>
                string.Equals(version.VersionCode, versionCode, StringComparison.Ordinal)));
        }

        public Task<bool> ProductExistsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            ProductExistsCallCount++;
            return Task.FromResult(_productIds.Contains(productId));
        }

        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_versions
                .Where(version =>
                    version.ProductVersionId == productVersionId &&
                    version.Status == ProductStatus.ACTIVE &&
                    version.IsPublic == true)
                .Select(version => new ProductVersionDetailReadModel
                {
                    ProductVersionId = version.ProductVersionId,
                    ProductId = version.ProductId,
                    VersionCode = version.VersionCode,
                    VersionName = version.VersionName,
                    VersionType = version.VersionType,
                    Material = version.Material,
                    Color = version.Color,
                    Width = version.Width,
                    Height = version.Height,
                    Depth = version.Depth,
                    EstimatedPrice = version.EstimatedPrice,
                    IsDefault = version.IsDefault,
                    IsPublic = version.IsPublic,
                    IsProjectSpecific = version.IsProjectSpecific,
                    Status = version.Status
                })
                .FirstOrDefault());
        }

        public Task SetDefaultAsync(
            ProductVersion productVersion,
            CancellationToken cancellationToken = default)
        {
            SetDefaultCallCount++;
            foreach (var version in _versions.Where(version => version.ProductId == productVersion.ProductId))
            {
                version.IsDefault = version.ProductVersionId == productVersion.ProductVersionId;
            }

            productVersion.IsDefault = true;
            return Task.CompletedTask;
        }

        public IQueryable<ProductVersion> Query() => _versions.AsQueryable();

        public Task<ProductVersion?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_versions.FirstOrDefault(version => version.ProductVersionId == id));
        }

        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductVersion>>(_versions);
        }

        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _versions.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default)
        {
            _versions.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(ProductVersion entity)
        {
        }

        public void Remove(ProductVersion entity)
        {
            _versions.Remove(entity);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
