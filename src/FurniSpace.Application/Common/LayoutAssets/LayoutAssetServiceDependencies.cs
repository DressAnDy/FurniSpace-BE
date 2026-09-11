using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.LayoutAssets;

public sealed class LayoutAssetServiceDependencies
{
    public LayoutAssetServiceDependencies(
        IFileStorageService storage,
        FileUploadSettings uploadSettings,
        FirebaseStorageSettings firebaseSettings)
    {
        Storage = storage;
        UploadSettings = uploadSettings;
        FirebaseSettings = firebaseSettings;
    }

    public IFileStorageService Storage { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }
}
