using FurniSpace.Application.Common.Storage;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.LayoutAssets;

public sealed class LayoutAssetServiceDependencies
{
    public LayoutAssetServiceDependencies(
        IFileStorageService storage,
        FileUploadSettings uploadSettings,
        FirebaseStorageSettings firebaseSettings,
        DirectFileUploadCoordinator directUploadCoordinator,
        CatalogDirectFileUploadService catalogDirectUpload)
    {
        Storage = storage;
        UploadSettings = uploadSettings;
        FirebaseSettings = firebaseSettings;
        DirectUploadCoordinator = directUploadCoordinator;
        CatalogDirectUpload = catalogDirectUpload;
    }

    public IFileStorageService Storage { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }

    public DirectFileUploadCoordinator DirectUploadCoordinator { get; }

    public CatalogDirectFileUploadService CatalogDirectUpload { get; }
}
