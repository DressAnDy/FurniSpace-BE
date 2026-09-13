using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProjectFileServiceDependencies(
    IUnitOfWork UnitOfWork,
    IFileStorageService Storage,
    IDirectFileUploadStorageService DirectUploadStorage,
    DirectFileUploadCoordinator DirectUploadCoordinator,
    FileUploadSettings UploadSettings,
    FirebaseStorageSettings FirebaseSettings);
