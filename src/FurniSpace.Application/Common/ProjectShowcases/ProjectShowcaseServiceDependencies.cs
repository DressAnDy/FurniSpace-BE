using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.ProjectShowcases;

public sealed class ProjectShowcaseServiceDependencies
{
    public ProjectShowcaseServiceDependencies(
        IFileStorageService storage,
        DirectFileUploadCoordinator directUploadCoordinator,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        INotificationDispatcher? notifications = null)
    {
        Storage = storage;
        DirectUploadCoordinator = directUploadCoordinator;
        UploadSettings = uploadSettings.Value;
        FirebaseSettings = firebaseSettings.Value;
        Notifications = notifications;
    }

    public IFileStorageService Storage { get; }

    public DirectFileUploadCoordinator DirectUploadCoordinator { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }

    public INotificationDispatcher? Notifications { get; }
}
