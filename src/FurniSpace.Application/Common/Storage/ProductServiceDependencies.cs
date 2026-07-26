using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Products;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProductServiceDependencies(
    IFileStorageService Storage,
    ISearchIndexService Search,
    IProductSearchIndexer ProductSearchIndexer,
    FileUploadSettings UploadSettings,
    ProductPreviewImageSettings PreviewSettings,
    FirebaseStorageSettings FirebaseSettings,
    ILogger<ProductService>? Logger = null);
