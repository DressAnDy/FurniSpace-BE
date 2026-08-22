using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Application.Interfaces.Search;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.MeasurementImages;

public sealed class MeasurementImageServiceDependencies
{
    public MeasurementImageServiceDependencies(
        IUnitOfWork unitOfWork,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        IProjectFileSearchIndexer? projectFileSearchIndexer = null)
    {
        UnitOfWork = unitOfWork;
        UploadSettings = uploadSettings.Value;
        FirebaseSettings = firebaseSettings.Value;
        ProjectFileSearchIndexer = projectFileSearchIndexer;
    }

    public IUnitOfWork UnitOfWork { get; }

    public FileUploadSettings UploadSettings { get; }

    public FirebaseStorageSettings FirebaseSettings { get; }

    public IProjectFileSearchIndexer? ProjectFileSearchIndexer { get; }
}
