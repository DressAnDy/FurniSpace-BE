#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.Products;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Tests;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Tests.TestDoubles;

public static class CatalogServiceTestHelper
{
    static CatalogServiceTestHelper()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    public static ProductService CreateProductService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService? storage = null,
        ProductPreviewImageSettings? previewSettings = null,
        ISearchIndexService? search = null,
        IProductSearchIndexer? productSearchIndexer = null,
        IBusinessTypeRepository? businessTypes = null,
        ICatalogRepository? catalog = null)
    {
        return new ProductService(
            products,
            catalog ?? new FakeCatalogRepository(),
            businessTypes ?? new AllowingBusinessTypeRepository(),
            files,
            new ProductServiceDependencies(
                storage ?? new NoOpFileStorageService(),
                search ?? new NoOpSearchIndexService(),
                productSearchIndexer ?? new NoOpProductSearchIndexer(),
                DefaultUploadSettings(),
                previewSettings ?? DefaultPreviewImageSettings(),
                DefaultFirebaseSettings()),
            TestUnitOfWork.ForSaveChanges(products.SaveChangesAsync));
    }

    public static ProductPreviewImageService CreateProductPreviewImageService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService? storage = null,
        ProductPreviewImageSettings? previewSettings = null,
        IUnitOfWork? unitOfWork = null)
    {
        return new ProductPreviewImageService(
            products,
            files,
            storage ?? new NoOpFileStorageService(),
            Options.Create(previewSettings ?? DefaultPreviewImageSettings()),
            Options.Create(DefaultFirebaseSettings()),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(products.SaveChangesAsync));
    }

    public static ProductVersionService CreateProductVersionService(
        IProductVersionRepository productVersions,
        IProjectFileRepository files,
        IFileStorageService? storage = null,
        ProductPreviewImageSettings? previewSettings = null,
        IProductSearchIndexer? productSearchIndexer = null,
        ICatalogRepository? catalog = null,
        IUnitOfWork? unitOfWork = null)
    {
        return new ProductVersionService(
            productVersions,
            catalog ?? new FakeCatalogRepository(),
            files,
            new ProductVersionFileUploadDependencies(
                storage ?? new NoOpFileStorageService(),
                DefaultUploadSettings(),
                previewSettings ?? DefaultPreviewImageSettings(),
                DefaultFirebaseSettings()),
            productSearchIndexer ?? new NoOpProductSearchIndexer(),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(productVersions.SaveChangesAsync));
    }

    public static FileUploadSettings DefaultUploadSettings()
    {
        return new FileUploadSettings
        {
            MaxFileSizeBytes = 1024 * 1024,
            AllowedExtensions = [".jpg", ".jpeg", ".glb"],
            AllowedMimeTypes = ["image/jpeg", "model/gltf-binary", "application/octet-stream"]
        };
    }

    public static ProductPreviewImageSettings DefaultPreviewImageSettings()
    {
        return new ProductPreviewImageSettings
        {
            MaxFileSizeBytes = 1024 * 1024
        };
    }

    public static FirebaseStorageSettings DefaultFirebaseSettings()
    {
        return new FirebaseStorageSettings
        {
            Bucket = "test-bucket",
            ProductFilesPrefix = "products",
            ProductVersionFilesPrefix = "product-versions"
        };
    }

    private sealed class NoOpFileStorageService : IFileStorageService
    {
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
            => Task.CompletedTask;
    }

    private sealed class AllowingBusinessTypeRepository : IBusinessTypeRepository
    {
        public Task AddAsync(BusinessType businessType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<BusinessType?> GetByIdAsync(int businessTypeId, CancellationToken cancellationToken = default)
            => Task.FromResult<BusinessType?>(null);

        public Task<BusinessType?> GetForUpdateAsync(int businessTypeId, CancellationToken cancellationToken = default)
            => Task.FromResult<BusinessType?>(null);

        public Task<IReadOnlyList<BusinessType>> GetByIdsAsync(
            IReadOnlyCollection<int> businessTypeIds,
            CancellationToken cancellationToken = default)
        {
            var items = businessTypeIds
                .Select(id => new BusinessType
                {
                    Id = id,
                    Code = $"TYPE_{id}",
                    Name = $"Business Type {id}",
                    Status = true
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<BusinessType>>(items);
        }

        public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<BusinessType>> GetPagedAsync(
            bool status,
            string? keyword,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BusinessType>>([]);

        public Task<int> CountAsync(bool status, string? keyword, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
