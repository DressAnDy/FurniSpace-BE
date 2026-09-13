using FurniSpace.Application.Services.Products;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProductServiceDependencies(
    IFileStorageService Storage,
    FileUploadSettings UploadSettings,
    ProductPreviewImageSettings PreviewSettings,
    FirebaseStorageSettings FirebaseSettings,
    DirectFileUploadCoordinator DirectUploadCoordinator,
    CatalogDirectFileUploadService CatalogDirectUpload,
    ILogger<ProductService>? Logger = null);
