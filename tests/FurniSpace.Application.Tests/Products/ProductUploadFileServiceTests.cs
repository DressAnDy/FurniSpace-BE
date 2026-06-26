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
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Products;

public sealed class ProductUploadFileServiceTests
{
    [Fact]
    public async Task UploadFileAsync_WithValidOtherType_UploadsAndPersistsFile()
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
            CreateUploadRequest("catalog.jpg", FileType.OTHER, description: " Catalog note "));

        Assert.Equal(201, result.Status);
        Assert.Equal("Product file uploaded successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("PRODUCT", result.Data.ReferenceType);
        Assert.Equal(productId, result.Data.ReferenceId);
        Assert.Equal(FileType.OTHER, result.Data.FileType);
        Assert.StartsWith($"products/{productId:D}/", storage.UploadRequest!.ObjectName, StringComparison.Ordinal);
        Assert.Single(repository.StoredFiles);
        Assert.Equal("Catalog note", repository.FileLinks[0].Description);
    }

    [Fact]
    public async Task UploadFileAsync_WithReferenceImageType_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("reference.jpg", FileType.REFERENCE_IMAGE));

        Assert.Equal(400, result.Status);
        Assert.Contains("File type is not allowed for this upload.", result.Errors!);
        Assert.Empty(repository.StoredFiles);
    }

    [Fact]
    public async Task UploadFileAsync_WithPreviewImage_AppendsToEndAndSetsPrimary()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        repository.SeedPreview(productId, 1);
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("brown.webp", FileType.PRODUCT_PREVIEW, "image/webp"));

        Assert.Equal(201, result.Status);
        Assert.Equal(2, result.Data!.DisplayOrder);
        Assert.False(result.Data.IsPrimary);
        Assert.Equal(FileType.PRODUCT_PREVIEW, result.Data.FileType);
        Assert.True(repository.FileLinks.Single(link => link.DisplayOrder == 1).IsPrimary == true);
    }

    [Fact]
    public async Task UploadFileAsync_WithPreviewDisplayOrderOne_BecomesCoverAndNormalizes()
    {
        var productId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        repository.SeedPreview(productId, 1, firstId);
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("cover.webp", FileType.PRODUCT_PREVIEW, "image/webp", displayOrder: 1));

        Assert.Equal(201, result.Status);
        Assert.Equal(1, result.Data!.DisplayOrder);
        Assert.True(result.Data.IsPrimary);
        Assert.Equal(2, repository.FileLinks.Single(link => link.FileId == firstId).DisplayOrder);
        Assert.False(repository.FileLinks.Single(link => link.FileId == firstId).IsPrimary);
    }

    [Fact]
    public async Task UploadFileAsync_WhenPreviewMaxCountReached_ReturnsMaxFilesExceeded()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        for (var index = 0; index < 5; index++)
        {
            repository.SeedPreview(productId, index + 1);
        }

        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("extra.webp", FileType.PRODUCT_PREVIEW, "image/webp"));

        Assert.Equal(409, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.MaxFilesExceeded, result.ErrorCode);
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidPreviewDisplayOrder_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            new CatalogFileTestRepository());

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("cover.webp", FileType.PRODUCT_PREVIEW, "image/webp", displayOrder: 0));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidDisplayOrder, result.ErrorCode);
    }

    [Fact]
    public async Task UploadFileAsync_WithPreviewDisplayOrderBeyondCount_NormalizesToEnd()
    {
        var productId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        repository.SeedPreview(productId, 1, firstId);
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("tail.webp", FileType.PRODUCT_PREVIEW, "image/webp", displayOrder: 99));

        Assert.Equal(201, result.Status);
        Assert.Equal(2, result.Data!.DisplayOrder);
        Assert.False(result.Data.IsPrimary);
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
            CreateUploadRequest("catalog.jpg", FileType.OTHER));

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.ProductNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UploadFileAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(Guid.NewGuid()),
            new CatalogFileTestRepository());

        var result = await service.UploadFileAsync(
            Guid.Empty,
            Guid.NewGuid(),
            CreateUploadRequest("catalog.jpg", FileType.OTHER));

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
    }

    [Fact]
    public async Task UploadFileAsync_WithEmptyUserId_ReturnsUnauthorized()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            new CatalogFileTestRepository());

        var result = await service.UploadFileAsync(
            productId,
            Guid.Empty,
            CreateUploadRequest("catalog.jpg", FileType.OTHER));

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
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

    [Fact]
    public async Task UploadFileAsync_WithInvalidExtension_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            new CatalogFileTestRepository());

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("catalog.exe", FileType.OTHER));

        Assert.Equal(400, result.Status);
        Assert.Contains("File extension is not allowed.", result.Errors!);
    }

    [Fact]
    public async Task UploadFileAsync_WithPreviewPdfMimeType_ReturnsInvalidFileType()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("catalog.pdf", FileType.PRODUCT_PREVIEW, "application/pdf"));

        Assert.Equal(415, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
        Assert.Empty(repository.StoredFiles);
    }

    [Fact]
    public async Task UploadFileAsync_WithOversizedPreview_ReturnsFileTooLarge()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            new CatalogFileTestRepository(),
            previewSettings: new ProductPreviewImageSettings
            {
                MaxCount = 5,
                MaxFileSizeBytes = 8,
                AllowedExtensions = [".webp"],
                AllowedMimeTypes = ["image/webp"]
            });

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("cover.webp", FileType.PRODUCT_PREVIEW, "image/webp", fileSizeBytes: 100));

        Assert.Equal(413, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task UploadFileAsync_WithValidWebpPreview_AcceptsFile()
    {
        var productId = Guid.NewGuid();
        var repository = new CatalogFileTestRepository();
        var service = CatalogServiceTestHelper.CreateProductService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadFileAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("hero.webp", FileType.PRODUCT_PREVIEW, "image/webp"));

        Assert.Equal(201, result.Status);
        Assert.Equal("image/webp", result.Data!.MimeType);
        Assert.Single(repository.StoredFiles);
    }

    private static UploadCatalogFileRequestDto CreateUploadRequest(
        string fileName,
        FileType fileType,
        string contentType = "image/jpeg",
        string? description = null,
        int? displayOrder = null,
        long fileSizeBytes = 12)
    {
        return new UploadCatalogFileRequestDto
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes("file-content")),
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            FileType = fileType,
            Description = description,
            DisplayOrder = displayOrder
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

        public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(Guid productId, CancellationToken cancellationToken = default)
            => ProductRepositorySearchStubs.GetSearchIndexItemAsync(productId, cancellationToken);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default)
            => ProductRepositorySearchStubs.GetSearchIndexPageAsync(page, limit, cancellationToken);

        public Task<ProductSearchResultReadModel> SearchPublicAsync(ProductSearchQueryReadModel query, CancellationToken cancellationToken = default)
            => ProductRepositorySearchStubs.SearchPublicAsync(query, cancellationToken);

        public Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(string query, int limit, CancellationToken cancellationToken = default)
            => ProductRepositorySearchStubs.SuggestPublicAsync(query, limit, cancellationToken);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(Guid productId, int limit, CancellationToken cancellationToken = default)
            => ProductRepositorySearchStubs.GetSimilarPublicAsync(productId, limit, cancellationToken);
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

        public void SeedPreview(Guid productId, int displayOrder, Guid? fileId = null)
        {
            var id = fileId ?? Guid.NewGuid();
            var now = DateTime.UtcNow;
            StoredFiles.Add(new StoredFile
            {
                FileId = id,
                OriginalFileName = "preview.webp",
                StoredFileName = $"{id:N}.webp",
                FileUrl = $"https://storage.example.com/{id:N}.webp",
                StoragePath = $"products/{productId:D}/{id:N}.webp",
                MimeType = "image/webp",
                FileSizeBytes = 100,
                Status = FileStatus.ACTIVE,
                UploadedAt = now
            });
            FileLinks.Add(new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = id,
                ReferenceType = CatalogFileReferenceTypes.Product,
                ReferenceId = productId,
                FileType = FileType.PRODUCT_PREVIEW,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                DisplayOrder = displayOrder,
                IsPrimary = displayOrder == 1,
                CreatedAt = now
            });
        }

        private List<FileLink> GetPreviewLinks(Guid productId) => FileLinks
            .Where(link =>
                link.ReferenceId == productId &&
                link.ReferenceType == CatalogFileReferenceTypes.Product &&
                link.FileType == FileType.PRODUCT_PREVIEW &&
                StoredFiles.Any(file => file.FileId == link.FileId && file.Status != FileStatus.ARCHIVED))
            .ToList();

        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(GetPreviewLinks(productId).Count);

        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>(
                GetPreviewLinks(productId)
                    .OrderBy(link => link.DisplayOrder ?? int.MaxValue)
                    .ToList());

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

        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);

        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
            Guid productId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductPreviewImageReadModel?>(null);

        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
            Guid productVersionId,
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

        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => ProjectFileRepositorySearchStubs.GetSearchIndexItemAsync(fileId, cancellationToken);

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => ProjectFileRepositorySearchStubs.GetSearchIndexPageAsync(page, limit, cancellationToken);

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
            Guid projectId,
            string query,
            int page,
            int limit,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => ProjectFileRepositorySearchStubs.SearchByProjectAsync(
                projectId,
                query,
                page,
                limit,
                customerVisibleOnly,
                customerAccountId,
                cancellationToken);

        public Task<int> CountSearchByProjectAsync(
            Guid projectId,
            string query,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => ProjectFileRepositorySearchStubs.CountSearchByProjectAsync(
                projectId,
                query,
                customerVisibleOnly,
                customerAccountId,
                cancellationToken);
    }
}
