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
        Assert.NotNull(result.Data);
        Assert.Equal(secondId, result.Data.Items[0].FileId);
        Assert.True(result.Data.Items[0].IsCover);
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
        Assert.Single(repository.StoredFiles);
        Assert.Equal(1, repository.FileLinks.Single(link => link.FileId == secondId).DisplayOrder);
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
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProductCategoryReadModel?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult<ProductCategoryReadModel?>(null);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(Guid categoryId, int page, int limit, bool includeDefaultVersion, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class PreviewImageTestRepository : IProjectFileRepository
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

        private List<FileLink> GetPreviewLinks(Guid productId) => FileLinks
            .Where(link => link.ReferenceId == productId && link.FileType == FileType.PRODUCT_PREVIEW &&
                           StoredFiles.Any(f => f.FileId == link.FileId && f.Status == FileStatus.ACTIVE))
            .ToList();

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) { StoredFiles.Add(entity); return Task.CompletedTask; }
        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) { FileLinks.Add(fileLink); return Task.CompletedTask; }
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>(FileLinks.Where(l => l.FileId == fileId).ToList());
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { foreach (var l in fileLinks.ToList()) FileLinks.Remove(l); }
        public void Remove(StoredFile entity) { StoredFiles.Remove(entity); }
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
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
    }
}
