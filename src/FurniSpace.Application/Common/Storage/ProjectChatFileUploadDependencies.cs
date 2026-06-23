using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Storage;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProjectChatFileUploadDependencies(
    IFileStorageService Storage,
    IFileUploadValidator FileUploadValidator,
    FirebaseStorageSettings FirebaseSettings);
