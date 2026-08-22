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

public sealed class ProductPreviewImageServiceTests
{
    [Fact]
    public async Task UploadAsync_WithValidRequest_AssignsDisplayOrderAndPersistsPreview()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", displayOrder: 1));

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.DisplayOrder);
        Assert.Equal(FileType.PRODUCT_PREVIEW, result.Data.FileType);
        Assert.Single(repository.StoredFiles);
        Assert.Equal(1, repository.FileLinks[0].DisplayOrder);
    }

    [Fact]
    public async Task UploadAsync_WhenMaxCountReached_ReturnsMaxFilesExceeded()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        for (var index = 0; index < 5; index++)
        {
            repository.SeedPreview(productId, index + 1);
        }

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadAsync(productId, Guid.NewGuid(), CreateUploadRequest("preview.jpg"));

        Assert.Equal(409, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.MaxFilesExceeded, result.ErrorCode);
    }

    [Fact]
    public async Task GetListAsync_ReturnsItemsSortedWithCoverFirst()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 2, fileId: Guid.NewGuid());
        repository.SeedPreview(productId, 1, fileId: Guid.NewGuid());

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.GetListAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.True(result.Data.Items[0].IsCover);
        Assert.Equal(1, result.Data.Items[0].DisplayOrder);
    }

    [Fact]
    public async Task ReorderAsync_WithFileIds_NormalizesDisplayOrder()
    {
        var productId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, firstId);
        repository.SeedPreview(productId, 2, secondId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [secondId, firstId] });

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(secondId, result.Data[0].FileId);
        Assert.Equal(1, result.Data[0].DisplayOrder);
        Assert.True(result.Data[0].IsPrimary);
        Assert.Equal(firstId, result.Data[1].FileId);
        Assert.False(result.Data[1].IsPrimary);
        Assert.True(repository.FileLinks.Single(link => link.FileId == secondId).IsPrimary == true);
    }

    [Fact]
    public async Task ReorderAsync_WithDuplicateFileIds_ReturnsDuplicateFileId()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, fileId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [fileId, fileId] });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.DuplicateFileId, result.ErrorCode);
    }

    [Fact]
    public async Task ReorderAsync_WithForeignFileId_ReturnsFileNotBelongToProduct()
    {
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var localId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, localId);
        repository.SeedPreview(otherProductId, 1, foreignId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [foreignId, localId] });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotBelongToProduct, result.ErrorCode);
    }

    [Fact]
    public async Task ReorderAsync_WithMissingPreview_ReturnsInvalidReorderPayload()
    {
        var productId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, firstId);
        repository.SeedPreview(productId, 2, secondId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [firstId] });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidReorderPayload, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPreviewAndReindexesRemaining()
    {
        var productId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, firstId);
        repository.SeedPreview(productId, 2, secondId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, firstId);

        Assert.Equal(200, result.Status);
        Assert.Equal(firstId, result.Data!.DeletedFileId);
        Assert.Equal(1, result.Data.RemainingCount);
        Assert.True(result.Data.Reindexed);
        Assert.Single(repository.StoredFiles);
        var remainingLink = repository.FileLinks.Single(link => link.FileId == secondId);
        Assert.Equal(1, remainingLink.DisplayOrder);
        Assert.True(remainingLink.IsPrimary == true);
    }

    [Fact]
    public async Task DeleteAsync_WhenCoverDeleted_NextImageBecomesPrimary()
    {
        var productId = Guid.NewGuid();
        var coverId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, coverId);
        repository.SeedPreview(productId, 2, secondId);
        repository.FileLinks.Single(link => link.FileId == coverId).IsPrimary = true;

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, coverId);

        Assert.Equal(200, result.Status);
        Assert.True(repository.FileLinks.Single(link => link.FileId == secondId).IsPrimary == true);
        Assert.Equal(1, repository.FileLinks.Single(link => link.FileId == secondId).DisplayOrder);
    }

    [Fact]
    public async Task DeleteAsync_WithForeignFile_ReturnsFileNotBelongToProduct()
    {
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(otherProductId, 1, foreignId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, foreignId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotBelongToProduct, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_WithNonPreviewFile_ReturnsInvalidFileType()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedProductFile(productId, fileId, FileType.OTHER);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, fileId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_WhenLastPreview_RemainingCountZeroAndNotReindexed()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, fileId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, fileId);

        Assert.Equal(200, result.Status);
        Assert.Equal(0, result.Data!.RemainingCount);
        Assert.False(result.Data.Reindexed);
        Assert.Empty(repository.FileLinks);
    }

    [Fact]
    public async Task GetListAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(Guid.NewGuid()),
            new PreviewImageTestRepository());

        var result = await service.GetListAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
    }

    [Fact]
    public async Task GetListAsync_WithMissingProduct_ReturnsNotFound()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(),
            new PreviewImageTestRepository());

        var result = await service.GetListAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
    }

    [Fact]
    public async Task UploadAsync_WithInvalidExtension_ReturnsUnsupportedMediaType()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.exe"));

        Assert.Equal(415, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task UploadAsync_WithOversizedFile_ReturnsPayloadTooLarge()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository(),
            previewSettings: new ProductPreviewImageSettings
            {
                MaxCount = 5,
                MaxFileSizeBytes = 8,
                AllowedExtensions = [".jpg"],
                AllowedMimeTypes = ["image/jpeg"]
            });

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", fileSizeBytes: 100));

        Assert.Equal(413, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_WithMissingPreview_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.DeleteAsync(productId, Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyFileId_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.DeleteAsync(productId, Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("File id is required.", result.Message);
    }

    [Fact]
    public async Task UploadAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(Guid.NewGuid()),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(Guid.Empty, Guid.NewGuid(), CreateUploadRequest("preview.jpg"));

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
    }

    [Fact]
    public async Task UploadAsync_WithEmptyUserId_ReturnsUnauthorized()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(productId, Guid.Empty, CreateUploadRequest("preview.jpg"));

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
    }

    [Fact]
    public async Task UploadAsync_WithMissingProduct_ReturnsNotFound()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUploadRequest("preview.jpg"));

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
    }

    [Fact]
    public async Task UploadAsync_WithoutDisplayOrder_AssignsNextAvailableOrder()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1);
        repository.SeedPreview(productId, 2);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadAsync(productId, Guid.NewGuid(), CreateUploadRequest("preview.jpg"));

        Assert.Equal(201, result.Status);
        Assert.Equal(3, result.Data!.DisplayOrder);
    }

    [Fact]
    public async Task UploadAsync_WithDisplayOrder_ShiftsExistingOrders()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1);
        repository.SeedPreview(productId, 2);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", displayOrder: 1));

        Assert.Equal(201, result.Status);
        Assert.Equal(1, repository.FileLinks.Count(link => link.DisplayOrder == 1));
        Assert.Equal(1, repository.FileLinks.Count(link => link.DisplayOrder == 2));
        Assert.Equal(1, repository.FileLinks.Count(link => link.DisplayOrder == 3));
    }

    [Fact]
    public async Task UploadAsync_WithInvalidMimeType_ReturnsUnsupportedMediaType()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", contentType: "application/pdf"));

        Assert.Equal(415, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task UploadAsync_WithEmptyFileName_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest(string.Empty));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task UploadAsync_WithZeroFileSize_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", fileSizeBytes: 0));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task UploadAsync_WithInvalidDisplayOrder_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.UploadAsync(
            productId,
            Guid.NewGuid(),
            CreateUploadRequest("preview.jpg", displayOrder: 99));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    [Fact]
    public async Task UploadAsync_WhenSaveFails_DeletesUploadedObject()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        var storage = new TrackingPreviewStorage();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository,
            storage,
            unitOfWork: TestUnitOfWork.ForFailingSaveChanges());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(productId, Guid.NewGuid(), CreateUploadRequest("preview.jpg")));

        Assert.NotNull(storage.DeletedObjectName);
    }

    [Fact]
    public async Task ReorderAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(Guid.NewGuid()),
            new PreviewImageTestRepository());

        var result = await service.ReorderAsync(
            Guid.Empty,
            new ReorderProductPreviewImagesRequestDto { FileIds = [Guid.NewGuid()] });

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
    }

    [Fact]
    public async Task ReorderAsync_WithMissingProduct_ReturnsNotFound()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(),
            new PreviewImageTestRepository());

        var result = await service.ReorderAsync(
            Guid.NewGuid(),
            new ReorderProductPreviewImagesRequestDto { FileIds = [Guid.NewGuid()] });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.ProductNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ReorderAsync_WithNoPreviews_ReturnsEmptyList()
    {
        var productId = Guid.NewGuid();
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            new PreviewImageTestRepository());

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [] });

        Assert.Equal(200, result.Status);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task ReorderAsync_WithUnknownFileId_ReturnsFileNotFound()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, fileId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [Guid.NewGuid(), fileId] });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ReorderAsync_WithInvalidFileIds_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1, fileId);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto { FileIds = [Guid.NewGuid()] });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ReorderAsync_WithNeitherPayload_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var repository = new PreviewImageTestRepository();
        repository.SeedPreview(productId, 1);

        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.ReorderAsync(
            productId,
            new ReorderProductPreviewImagesRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidReorderPayload, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(Guid.NewGuid()),
            new PreviewImageTestRepository());

        var result = await service.DeleteAsync(Guid.Empty, Guid.NewGuid());

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_WithMissingProduct_ReturnsNotFound()
    {
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(),
            new PreviewImageTestRepository());

        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenStoredFileMissing_ReturnsPreviewNotFound()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new OrphanPreviewTestRepository(productId, fileId);
        var service = CatalogServiceTestHelper.CreateProductPreviewImageService(
            new StubProductRepository(productId),
            repository);

        var result = await service.DeleteAsync(productId, fileId);

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductPreviewImageErrorCodes.FileNotFound, result.ErrorCode);
    }

    private static UploadProductPreviewImageRequestDto CreateUploadRequest(
        string fileName,
        string contentType = "image/jpeg",
        long fileSizeBytes = 12,
        int? displayOrder = null)
    {
        return new UploadProductPreviewImageRequestDto
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes("file-content")),
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            DisplayOrder = displayOrder
        };
    }

    private sealed class StubProductRepository : IProductRepository
    {
        private readonly HashSet<Guid> _productIds;

        public StubProductRepository(params Guid[] productIds) => _productIds = productIds.ToHashSet();

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
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(int page, int limit, IReadOnlyCollection<int>? businessTypeIds = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountAsync(IReadOnlyCollection<int>? businessTypeIds = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
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

    private class PreviewImageTestRepository : IProjectFileRepository
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
                OriginalFileName = "preview.jpg",
                StoredFileName = $"{id:N}.jpg",
                FileUrl = $"https://storage.example.com/{id:N}.jpg",
                StoragePath = $"products/{productId:D}/{id:N}.jpg",
                MimeType = "image/jpeg",
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

        public void SeedProductFile(Guid productId, Guid fileId, FileType fileType)
        {
            var now = DateTime.UtcNow;
            StoredFiles.Add(new StoredFile
            {
                FileId = fileId,
                OriginalFileName = "catalog.jpg",
                StoredFileName = $"{fileId:N}.jpg",
                FileUrl = $"https://storage.example.com/{fileId:N}.jpg",
                StoragePath = $"products/{productId:D}/{fileId:N}.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 100,
                Status = FileStatus.ACTIVE,
                UploadedAt = now
            });
            FileLinks.Add(new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = fileId,
                ReferenceType = CatalogFileReferenceTypes.Product,
                ReferenceId = productId,
                FileType = fileType,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                CreatedAt = now
            });
        }

        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(GetPreviewLinks(productId).Count);

        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            var items = GetPreviewLinks(productId)
                .Join(StoredFiles, link => link.FileId, file => file.FileId, (link, file) => new ProductPreviewImageReadModel
                {
                    FileId = file.FileId,
                    FileLinkId = link.FileLinkId,
                    ProductId = productId,
                    FileType = FileType.PRODUCT_PREVIEW,
                    FileUrl = file.FileUrl,
                    MimeType = file.MimeType,
                    FileSizeBytes = file.FileSizeBytes,
                    DisplayOrder = link.DisplayOrder ?? 0,
                    CreatedAt = link.CreatedAt ?? file.UploadedAt,
                    StoragePath = file.StoragePath
                })
                .OrderBy(item => item.DisplayOrder)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>(items);
        }

        public async Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
            Guid productId, Guid fileId, CancellationToken cancellationToken = default)
        {
            var items = await GetProductPreviewFilesAsync(productId, cancellationToken);
            return items.FirstOrDefault(item => item.FileId == fileId);
        }

        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
            Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>(GetPreviewLinks(productId).OrderBy(l => l.DisplayOrder).ToList());

        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
            Guid productVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);

        private List<FileLink> GetPreviewLinks(Guid productId) => FileLinks
            .Where(link => link.ReferenceId == productId && link.FileType == FileType.PRODUCT_PREVIEW &&
                           StoredFiles.Any(f => f.FileId == link.FileId && f.Status == FileStatus.ACTIVE))
            .ToList();

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) { StoredFiles.Add(entity); return Task.CompletedTask; }
        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) { FileLinks.Add(fileLink); return Task.CompletedTask; }
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>(FileLinks.Where(link => link.FileId == fileId).ToList());
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { foreach (var l in fileLinks.ToList()) FileLinks.Remove(l); }
        public void Remove(StoredFile entity) { StoredFiles.Remove(entity); }
        public virtual Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFile?>(StoredFiles.FirstOrDefault(f => f.FileId == id));
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(string referenceType, IReadOnlyList<Guid> referenceIds, bool customerVisibleOnly, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<FileMetadataReadModel?>(null);
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new FileReferencePageReadModel());
        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default) => Task.FromResult<FileLinkReadModel?>(null);
        public IQueryable<StoredFile> Query() => StoredFiles.AsQueryable();
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>(StoredFiles);
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

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

    private sealed class TrackingPreviewStorage : IFileStorageService
    {
        public string? DeletedObjectName { get; private set; }

        public Task<StorageUploadResult> UploadAsync(
            StorageUploadRequest request,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class OrphanPreviewTestRepository : PreviewImageTestRepository
    {
        public OrphanPreviewTestRepository(Guid productId, Guid fileId)
        {
            SeedPreview(productId, 1, fileId);
        }

        public override Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFile?>(null);
    }
}
