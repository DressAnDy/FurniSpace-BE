using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.ProjectShowcases;

public sealed class ProjectShowcaseServiceDependencies
{
    public ProjectShowcaseServiceDependencies(
        IFileStorageService storage,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        Storage = storage;
        UploadSettings = uploadSettings.Value;
        FirebaseSettings = firebaseSettings.Value;
    }

    public IFileStorageService Storage { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }
}
