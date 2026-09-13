using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProjectChatFileUploadDependencies(
    IFileStorageService Storage,
    IFileUploadValidator FileUploadValidator,
    DirectFileUploadCoordinator DirectUploadCoordinator,
    FirebaseStorageSettings FirebaseSettings);
