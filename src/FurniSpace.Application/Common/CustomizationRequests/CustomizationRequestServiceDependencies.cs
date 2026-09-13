using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.CustomizationRequests;

public sealed record CustomizationRequestServiceDependencies(
    ICustomizationRequestRepository CustomizationRequests,
    ICustomizationRequestVersionRepository CustomizationRequestVersions,
    IProposalRepository Proposals,
    IProjectRepository Projects,
    IProductVersionRepository ProductVersions,
    IProjectFileRepository ProjectFiles,
    INotificationDispatcher Dispatcher,
    IUnitOfWork UnitOfWork);
