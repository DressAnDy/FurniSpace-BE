#nullable enable

using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Mappings;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.Products;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Tests.TestDoubles;

public static class CatalogServiceTestHelper
{
    static CatalogServiceTestHelper()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(CatalogFileMappingConfig).Assembly);
    }

    public static ProductService CreateProductService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService? storage = null,
        ProductPreviewImageSettings? previewSettings = null,
        ISearchIndexService? search = null,
        IProductSearchIndexer? productSearchIndexer = null)
    {
        return new ProductService(
            products,
            files,
            storage ?? new NoOpFileStorageService(),
            Options.Create(DefaultUploadSettings()),
            Options.Create(previewSettings ?? DefaultPreviewImageSettings()),
            Options.Create(DefaultFirebaseSettings()),
            search ?? new NoOpSearchIndexService(),
            productSearchIndexer ?? new NoOpProductSearchIndexer(),
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
        IProductSearchIndexer? productSearchIndexer = null)
    {
        return new ProductVersionService(
            productVersions,
            files,
            storage ?? new NoOpFileStorageService(),
            Options.Create(DefaultUploadSettings()),
            Options.Create(previewSettings ?? DefaultPreviewImageSettings()),
            Options.Create(DefaultFirebaseSettings()),
            productSearchIndexer ?? new NoOpProductSearchIndexer(),
            TestUnitOfWork.ForSaveChanges(productVersions.SaveChangesAsync));
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
}
