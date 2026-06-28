using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProductServiceDependencies(
    IFileStorageService Storage,
    ISearchIndexService Search,
    IProductSearchIndexer ProductSearchIndexer,
    FileUploadSettings UploadSettings,
    ProductPreviewImageSettings PreviewSettings,
    FirebaseStorageSettings FirebaseSettings);
