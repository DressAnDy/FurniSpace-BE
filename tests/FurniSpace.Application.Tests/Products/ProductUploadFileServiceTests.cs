#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Xunit;

namespace FurniSpace.Application.Tests.Products;

public sealed class ProductUploadFileServiceTests
{
    [Fact]
    public async Task UploadFileAsync_WithValidRequest_UploadsAndPersistsFile()
    {
        var adminId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var storage = new CatalogFileTestStorage();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository,
            storage);

        var result = await service.UploadFileAsync(
            productId,
            adminId,
            CreateUploadRequest("reference.jpg", FileType.REFERENCE_IMAGE, description: " Reference "));

        Assert.Equal(201, result.Status);
        Assert.Equal("Product file uploaded successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("PRODUCT", result.Data.ReferenceType);
        Assert.Equal(productId, result.Data.ReferenceId);
        Assert.Equal(FileType.REFERENCE_IMAGE, result.Data.FileType);
        Assert.StartsWith($"products/{productId:D}/", storage.UploadRequest!.ObjectName, StringComparison.Ordinal);
        Assert.Single(repository.StoredFiles);
        Assert.Equal("Reference", repository.FileLinks[0].Description);
    }

    [Fact]
    public async Task UploadFileAsync_WithProductPreviewType_ReturnsUsePreviewFilesEndpoint()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", FileType.PRODUCT_PREVIEW));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.UsePreviewFilesEndpoint, result.ErrorCode);
        Assert.Empty(repository.StoredFiles);
    }

    [Fact]
    public async Task UploadFileAsync_WithMissingProduct_ReturnsNotFound()
    {
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(),
            repository);

        var result = await service.UploadFileAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateUploadRequest("reference.jpg", FileType.REFERENCE_IMAGE));

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidFileType_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("model.glb", FileType.MODEL_3D));

        Assert.Equal(400, result.Status);
        Assert.Contains("File type is not allowed for this upload.", result.Errors!);
    }

    private static UploadCatalogFileRequestDto CreateUploadRequest(
        string fileName,
        FileType fileType,
        string contentType = "image/jpeg",
        string? description = null)
    {
        return new UploadCatalogFileRequestDto
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes("file-content")),
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSizeBytes = 12,
            FileType = fileType,
            Description = description
        };
    }

    private sealed class StubProductRepository : IProductRepository
    {
        private readonly HashSet<Guid> _productIds;

        public StubProductRepository(params Guid[] productIds)
        {
            _productIds = productIds.ToHashSet();
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_productIds.Contains(id) ? new Product { ProductId = id } : null);

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

    private sealed class CatalogFileTestStorage : IFileStorageService
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

    private sealed class CatalogFileTestRepository : IProjectFileRepository
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
}
