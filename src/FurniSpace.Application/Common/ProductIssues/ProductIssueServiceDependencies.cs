using FurniSpace.Application.Common.Storage;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Common.ProductIssues;

public sealed record ProductIssueServiceDependencies(
    IUnitOfWork UnitOfWork,
    IFileStorageService Storage,
    IFileUploadValidator FileUploadValidator,
    DirectFileUploadCoordinator DirectUploadCoordinator,
    FirebaseStorageSettings FirebaseSettings);
