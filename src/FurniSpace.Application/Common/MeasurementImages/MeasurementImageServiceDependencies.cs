using FurniSpace.Application.Common.Storage;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.MeasurementImages;

public sealed class MeasurementImageServiceDependencies
{
    public MeasurementImageServiceDependencies(
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        DirectFileUploadCoordinator directUploadCoordinator,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        UnitOfWork = unitOfWork;
        Storage = storage;
        DirectUploadCoordinator = directUploadCoordinator;
        UploadSettings = uploadSettings.Value;
        FirebaseSettings = firebaseSettings.Value;
    }

    public IUnitOfWork UnitOfWork { get; }

    public IFileStorageService Storage { get; }

    public DirectFileUploadCoordinator DirectUploadCoordinator { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }
}
