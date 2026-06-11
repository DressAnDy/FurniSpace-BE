#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Mappings;
using FurniSpace.Application.Services.ProjectFiles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectFiles;

public sealed class ProjectFileServiceTests
{
    public ProjectFileServiceTests()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(ProjectFileMappingConfig).Assembly);
    }

    [Fact]
    public async Task UploadProjectFileAsync_WithCustomerProjectAccess_UploadsAndPersistsFile()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = "CUSTOMER",
            ProjectAccess = new ProjectFileAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = customerId,
                Status = ProjectStatus.SUBMITTED
            }
        };
        var storage = new FakeFileStorageService();
        var service = CreateService(repository, storage);

        var result = await service.UploadProjectFileAsync(
            projectId,
            customerId,
            CreateUploadRequest("shop-reference.jpg", FileType.REFERENCE_IMAGE, visibility: null, note: " Reference image "));

        Assert.Equal(201, result.Status);
        Assert.Equal("Project file uploaded successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal("shop-reference.jpg", result.Data.OriginalFileName);
        Assert.Equal(FileType.REFERENCE_IMAGE, result.Data.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, result.Data.Visibility);
        Assert.Equal(customerId, result.Data.UploadedBy);
        Assert.StartsWith($"projects/{projectId:D}/", result.Data.StoragePath, StringComparison.Ordinal);
        Assert.EndsWith(".jpg", result.Data.FileName, StringComparison.Ordinal);

        Assert.Single(repository.StoredFiles);
        Assert.Single(repository.FileLinks);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(customerId, repository.StoredFiles[0].UploadedBy);
        Assert.Equal("jpg", repository.StoredFiles[0].FileExtension);
        Assert.Equal(FileStatus.ACTIVE, repository.StoredFiles[0].Status);
        Assert.Equal(projectId, repository.FileLinks[0].ReferenceId);
        Assert.Equal("PROJECT", repository.FileLinks[0].ReferenceType);
        Assert.Equal("Reference image", repository.FileLinks[0].Description);

        Assert.NotNull(storage.UploadRequest);
        Assert.Equal("image/jpeg", storage.UploadRequest.ContentType);
        Assert.Equal(repository.StoredFiles[0].StoragePath, storage.UploadRequest.ObjectName);
    }

    [Fact]
    public async Task UploadProjectFileAsync_WithInvalidFile_ReturnsValidationErrors()
    {
        var repository = new FakeProjectFileRepository();
        var service = CreateService(repository, new FakeFileStorageService());

        var result = await service.UploadProjectFileAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UploadProjectFileRequestDto
            {
                Content = Stream.Null,
                OriginalFileName = "invalid.exe",
                ContentType = "application/x-msdownload",
                FileSizeBytes = 0,
                FileType = FileType.OTHER
            });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("File is required.", result.Errors!);
        Assert.Contains("File size must be greater than zero.", result.Errors!);
        Assert.Contains("File extension is not allowed.", result.Errors!);
        Assert.Contains("File MIME type is not allowed.", result.Errors!);
        Assert.Empty(repository.StoredFiles);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadProjectFileAsync_WithUnassignedSales_ReturnsForbidden()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = "SALES",
            ProjectAccess = new ProjectFileAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = Guid.NewGuid()
            }
        };
        var storage = new FakeFileStorageService();
        var service = CreateService(repository, storage);

        var result = await service.UploadProjectFileAsync(
            projectId,
            salesId,
            CreateUploadRequest("floor-plan.pdf", FileType.PDF_DRAWING));

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to upload files to this project.", result.Message);
        Assert.Null(storage.UploadRequest);
        Assert.Empty(repository.StoredFiles);
    }

    [Fact]
    public async Task UploadProjectFileAsync_WithMissingRole_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = null,
            ProjectAccess = new ProjectFileAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = customerId
            }
        };
        var storage = new FakeFileStorageService();
        var service = CreateService(repository, storage);

        var result = await service.UploadProjectFileAsync(
            projectId,
            customerId,
            CreateUploadRequest("floor-plan.pdf", FileType.PDF_DRAWING));

        Assert.Equal(403, result.Status);
        Assert.Equal("Authenticated account is not active or has no role.", result.Message);
        Assert.Null(storage.UploadRequest);
        Assert.Empty(repository.StoredFiles);
    }

    [Fact]
    public async Task GetProjectFilesAsync_ForCustomer_SetsCustomerVisibleFilter()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = "CUSTOMER",
            ProjectAccess = new ProjectFileAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = customerId
            },
            FileReferencePage = new FileReferencePageReadModel
            {
                Total = 1,
                Items =
                [
                    CreateMetadata(projectId, customerId, FileVisibility.CUSTOMER_VISIBLE)
                ]
            }
        };
        var service = CreateService(repository, new FakeFileStorageService());

        var result = await service.GetProjectFilesAsync(
            projectId,
            customerId,
            new ProjectFilesQueryDto
            {
                FileType = FileType.REFERENCE_IMAGE,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                Page = 2,
                Limit = 5
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(5, result.Data.Limit);
        Assert.NotNull(repository.LastReferenceQuery);
        Assert.Equal("PROJECT", repository.LastReferenceQuery.ReferenceType);
        Assert.Equal(projectId, repository.LastReferenceQuery.ReferenceId);
        Assert.Equal(FileType.REFERENCE_IMAGE, repository.LastReferenceQuery.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, repository.LastReferenceQuery.Visibility);
        Assert.True(repository.LastReferenceQuery.CustomerVisibleOnly);
        Assert.Equal(customerId, repository.LastReferenceQuery.CustomerAccountId);
    }

    [Fact]
    public async Task GetFilesByReferenceAsync_WithUnsupportedReferenceType_ReturnsBadRequest()
    {
        var repository = new FakeProjectFileRepository();
        var service = CreateService(repository, new FakeFileStorageService());

        var result = await service.GetFilesByReferenceAsync(
            Guid.NewGuid(),
            new FilesByReferenceQueryDto
            {
                ReferenceType = "UNKNOWN",
                ReferenceId = Guid.NewGuid()
            });

        Assert.Equal(400, result.Status);
        Assert.Contains("Reference type is not supported.", result.Errors!);
        Assert.Equal(0, repository.GetReferenceProjectAccessCallCount);
    }

    [Fact]
    public async Task GetFilesByReferenceAsync_WithProductReference_AllowsAnonymousAccess()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            FileReferencePage = new FileReferencePageReadModel
            {
                Total = 1,
                Items =
                [
                    new FileMetadataReadModel
                    {
                        FileId = Guid.NewGuid(),
                        OriginalFileName = "lamp-preview.jpg",
                        FileType = FileType.PRODUCT_PREVIEW,
                        Visibility = FileVisibility.CUSTOMER_VISIBLE,
                        Status = FileStatus.ACTIVE
                    }
                ]
            }
        };
        var products = new FakeCatalogProductRepository { ExistingProductIds = [productId] };
        var service = CreateService(repository, new FakeFileStorageService(), products);

        var result = await service.GetFilesByReferenceAsync(
            Guid.Empty,
            new FilesByReferenceQueryDto
            {
                ReferenceType = "PRODUCT",
                ReferenceId = productId
            });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.NotNull(repository.LastReferenceQuery);
        Assert.Equal("PRODUCT", repository.LastReferenceQuery.ReferenceType);
        Assert.Equal(productId, repository.LastReferenceQuery.ReferenceId);
        Assert.True(repository.LastReferenceQuery.CustomerVisibleOnly);
        Assert.Equal(0, repository.GetReferenceProjectAccessCallCount);
    }

    [Fact]
    public async Task GetFilesByReferenceAsync_WithProductReference_WhenProductMissing_ReturnsNotFound()
    {
        var repository = new FakeProjectFileRepository();
        var service = CreateService(repository, new FakeFileStorageService(), new FakeCatalogProductRepository());

        var result = await service.GetFilesByReferenceAsync(
            Guid.Empty,
            new FilesByReferenceQueryDto
            {
                ReferenceType = "PRODUCT",
                ReferenceId = Guid.NewGuid()
            });

        Assert.Equal(404, result.Status);
        Assert.Equal("Referenced object not found.", result.Message);
    }

    [Fact]
    public async Task GetFilesByReferenceAsync_WithAdminRole_DoesNotFilterCustomerVisibleOnly()
    {
        var adminId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = "ADMIN",
            FileReferencePage = new FileReferencePageReadModel { Total = 0, Items = [] }
        };
        var products = new FakeCatalogProductRepository { ExistingProductIds = [productId] };
        var service = CreateService(repository, new FakeFileStorageService(), products);

        var result = await service.GetFilesByReferenceAsync(
            adminId,
            new FilesByReferenceQueryDto
            {
                ReferenceType = "PRODUCT",
                ReferenceId = productId,
                Visibility = FileVisibility.STAFF_ONLY
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(repository.LastReferenceQuery);
        Assert.False(repository.LastReferenceQuery.CustomerVisibleOnly);
        Assert.Equal(FileVisibility.STAFF_ONLY, repository.LastReferenceQuery.Visibility);
    }

    [Fact]
    public async Task GetFileDetailAsync_ForCustomerStaffOnlyFile_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectFileRepository
        {
            RoleName = "CUSTOMER"
        };
        repository.FileMetadata[projectId] = CreateMetadata(projectId, Guid.NewGuid(), FileVisibility.STAFF_ONLY, customerId);
        var service = CreateService(repository, new FakeFileStorageService());

        var result = await service.GetFileDetailAsync(projectId, customerId);

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view this file.", result.Message);
    }

    [Fact]
    public async Task DeleteFileAsync_WithAllowedUploader_HardDeletesStorageLinksAndMetadata()
    {
        var uploaderId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var storedFile = CreateStoredFile(fileId, uploaderId);
        var link = new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = fileId,
            ReferenceType = "PROJECT",
            ReferenceId = Guid.NewGuid()
        };
        var repository = new FakeProjectFileRepository
        {
            RoleName = "SALES"
        };
        repository.Entities[fileId] = storedFile;
        repository.FileMetadata[fileId] = CreateMetadata(fileId, uploaderId, FileVisibility.STAFF_ONLY, projectStatus: ProjectStatus.IN_CONSULTATION);
        repository.FileLinkEntities.Add(link);
        var storage = new FakeFileStorageService();
        var service = CreateService(repository, storage);

        var result = await service.DeleteFileAsync(fileId, uploaderId);

        Assert.Equal(200, result.Status);
        Assert.Equal("File deleted successfully.", result.Message);
        Assert.Equal(fileId, result.Data!.FileId);
        Assert.Equal(storedFile.StoragePath, storage.DeletedObjectName);
        Assert.Single(repository.RemovedFileLinks);
        Assert.Same(link, repository.RemovedFileLinks[0]);
        Assert.Same(storedFile, repository.RemovedFile);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task ArchiveFileAsync_WithActiveFile_SetsArchivedStatus()
    {
        var uploaderId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var storedFile = CreateStoredFile(fileId, uploaderId);
        var repository = new FakeProjectFileRepository
        {
            RoleName = "ADMIN"
        };
        repository.Entities[fileId] = storedFile;
        repository.FileMetadata[fileId] = CreateMetadata(fileId, uploaderId, FileVisibility.STAFF_ONLY, projectStatus: ProjectStatus.ORDER_CONFIRMED);
        var service = CreateService(repository, new FakeFileStorageService());

        var result = await service.ArchiveFileAsync(
            fileId,
            Guid.NewGuid(),
            new ArchiveFileRequestDto { Reason = "Outdated" });

        Assert.Equal(200, result.Status);
        Assert.Equal(FileStatus.ARCHIVED, result.Data!.Status);
        Assert.Equal(FileStatus.ARCHIVED, storedFile.Status);
        Assert.NotNull(storedFile.ArchivedAt);
        Assert.Same(storedFile, repository.UpdatedFile);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    private static ProjectFileService CreateService(
        FakeProjectFileRepository repository,
        FakeFileStorageService storage,
        FakeCatalogProductRepository? products = null,
        FakeCatalogProductVersionRepository? productVersions = null)
    {
        return new ProjectFileService(
            repository,
            products ?? new FakeCatalogProductRepository(),
            productVersions ?? new FakeCatalogProductVersionRepository(),
            storage,
            Options.Create(new FileUploadSettings
            {
                MaxFileSizeBytes = 1024 * 1024,
                AllowedExtensions = [".jpg", ".jpeg", ".pdf"],
                AllowedMimeTypes = ["image/jpeg", "application/pdf"]
            }),
            Options.Create(new FirebaseStorageSettings
            {
                Bucket = "test-bucket",
                ProjectFilesPrefix = "projects"
            }));
    }

    private static UploadProjectFileRequestDto CreateUploadRequest(
        string fileName,
        FileType fileType,
        FileVisibility? visibility = FileVisibility.STAFF_ONLY,
        string? note = null)
    {
        return new UploadProjectFileRequestDto
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes("file-content")),
            OriginalFileName = fileName,
            ContentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "image/jpeg",
            FileSizeBytes = 12,
            FileType = fileType,
            Visibility = visibility,
            Note = note
        };
    }

    private static FileMetadataReadModel CreateMetadata(
        Guid fileId,
        Guid uploadedBy,
        FileVisibility visibility,
        Guid? customerId = null,
        ProjectStatus projectStatus = ProjectStatus.IN_CONSULTATION)
    {
        var projectId = Guid.NewGuid();
        return new FileMetadataReadModel
        {
            FileId = fileId,
            FileLinkId = Guid.NewGuid(),
            ReferenceType = "PROJECT",
            ReferenceId = projectId,
            OriginalFileName = "shop-reference.jpg",
            StoredFileName = $"{fileId:N}.jpg",
            FileType = FileType.REFERENCE_IMAGE,
            MimeType = "image/jpeg",
            FileSizeBytes = 204800,
            StoragePath = $"projects/{projectId:D}/{fileId:N}.jpg",
            FileUrl = "https://storage.example.com/file.jpg",
            Visibility = visibility,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            Status = FileStatus.ACTIVE,
            ProjectAccess = new ProjectFileAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = customerId ?? uploadedBy,
                AssignedSalesId = uploadedBy,
                AssignedDesignerId = uploadedBy,
                Status = projectStatus
            }
        };
    }

    private static StoredFile CreateStoredFile(Guid fileId, Guid uploadedBy)
    {
        return new StoredFile
        {
            FileId = fileId,
            UploadedBy = uploadedBy,
            OriginalFileName = "shop-reference.jpg",
            StoredFileName = $"{fileId:N}.jpg",
            FileUrl = "https://storage.example.com/file.jpg",
            StoragePath = $"projects/project-id/{fileId:N}.jpg",
            MimeType = "image/jpeg",
            FileExtension = "jpg",
            FileSizeBytes = 204800,
            Status = FileStatus.ACTIVE,
            UploadedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public StorageUploadRequest? UploadRequest { get; private set; }
        public string? DeletedObjectName { get; private set; }

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
        {
            DeletedObjectName = objectName;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectFileRepository : IProjectFileRepository
    {
        public ProjectFileAccessReadModel? ProjectAccess { get; init; }
        public ProjectFileAccessReadModel? ReferenceProjectAccess { get; init; }
        public string? RoleName { get; init; }
        public FileReferencePageReadModel FileReferencePage { get; init; } = new();
        public Dictionary<Guid, StoredFile> Entities { get; } = [];
        public Dictionary<Guid, FileMetadataReadModel> FileMetadata { get; } = [];
        public List<StoredFile> StoredFiles { get; } = [];
        public List<FileLink> FileLinks { get; } = [];
        public List<FileLink> FileLinkEntities { get; } = [];
        public List<FileLink> RemovedFileLinks { get; } = [];
        public StoredFile? RemovedFile { get; private set; }
        public StoredFile? UpdatedFile { get; private set; }
        public FileReferenceQueryReadModel? LastReferenceQuery { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int GetReferenceProjectAccessCallCount { get; private set; }

        public IQueryable<StoredFile> Query()
        {
            return Entities.Values.Concat(StoredFiles).AsQueryable();
        }

        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Entities.TryGetValue(id, out var entity);
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredFile>>(Query().ToList());
        }

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
        {
            StoredFiles.Add(entity);
            Entities[entity.FileId] = entity;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                StoredFiles.Add(entity);
                Entities[entity.FileId] = entity;
            }

            return Task.CompletedTask;
        }

        public void Update(StoredFile entity)
        {
            UpdatedFile = entity;
        }

        public void Remove(StoredFile entity)
        {
            RemovedFile = entity;
            Entities.Remove(entity.FileId);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProjectAccess);
        }

        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
            string referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
        {
            GetReferenceProjectAccessCallCount++;
            return Task.FromResult(ReferenceProjectAccess ?? ProjectAccess);
        }

        public Task<string?> GetAccountRoleNameAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RoleName);
        }

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        {
            FileLinks.Add(fileLink);
            FileLinkEntities.Add(fileLink);
            return Task.CompletedTask;
        }

        public Task<FileMetadataReadModel?> GetFileMetadataAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            FileMetadata.TryGetValue(fileId, out var metadata);
            return Task.FromResult(metadata);
        }

        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
            FileReferenceQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastReferenceQuery = query;
            return Task.FromResult(FileReferencePage);
        }

        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            var links = FileLinkEntities.Where(link => link.FileId == fileId).ToList();
            return Task.FromResult<IReadOnlyList<FileLink>>(links);
        }

        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
        {
            RemovedFileLinks.AddRange(fileLinks);
            foreach (var link in fileLinks)
            {
                FileLinkEntities.Remove(link);
            }
        }

        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
    }

    private sealed class FakeCatalogProductRepository : IProductRepository
    {
        public HashSet<Guid> ExistingProductIds { get; init; } = [];

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingProductIds.Contains(id) ? new Product { ProductId = id } : null);

        public IQueryable<Product> Query() => Enumerable.Empty<Product>().AsQueryable();
        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddAsync(Product entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Product> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Product entity) { }
        public void Remove(Product entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<bool> ProductCodeExistsAsync(string productCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductDetailReadModel?> GetDetailAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductDetailReadModel?>(null);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProductCategoryReadModel?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult<ProductCategoryReadModel?>(null);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(Guid categoryId, int page, int limit, bool includeDefaultVersion, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeCatalogProductVersionRepository : IProductVersionRepository
    {
        public HashSet<Guid> ExistingVersionIds { get; init; } = [];

        public Task<ProductVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingVersionIds.Contains(id) ? new ProductVersion { ProductVersionId = id, ProductId = Guid.NewGuid() } : null);

        public IQueryable<ProductVersion> Query() => Enumerable.Empty<ProductVersion>().AsQueryable();
        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductVersion>>([]);
        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersionDetailReadModel?>(null);
        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
