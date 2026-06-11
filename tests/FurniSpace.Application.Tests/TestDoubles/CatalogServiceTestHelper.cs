#nullable enable

using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Catalog;
using FurniSpace.Application.Mappings;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.Products;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
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
        IFileStorageService? storage = null)
    {
        return new ProductService(
            products,
            files,
            CreateCatalogReferenceFileUploader(files, storage),
            Options.Create(DefaultFirebaseSettings()));
    }

    public static ProductVersionService CreateProductVersionService(
        IProductVersionRepository productVersions,
        IProjectFileRepository files,
        IFileStorageService? storage = null)
    {
        return new ProductVersionService(
            productVersions,
            files,
            CreateCatalogReferenceFileUploader(files, storage),
            Options.Create(DefaultFirebaseSettings()));
    }

    private static CatalogReferenceFileUploader CreateCatalogReferenceFileUploader(
        IProjectFileRepository files,
        IFileStorageService? storage)
    {
        var uploadRules = new CatalogFileUploadRules(
            Options.Create(DefaultUploadSettings()),
            Options.Create(DefaultFirebaseSettings()));

        return new CatalogReferenceFileUploader(
            files,
            storage ?? new NoOpFileStorageService(),
            uploadRules);
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
