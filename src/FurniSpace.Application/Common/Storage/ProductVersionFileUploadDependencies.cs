using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProductVersionFileUploadDependencies(
    IFileStorageService Storage,
    FileUploadSettings UploadSettings,
    ProductPreviewImageSettings PreviewSettings,
    FirebaseStorageSettings FirebaseSettings);
