using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Projects;

public sealed record ProjectServiceDependencies(
    IUnitOfWork UnitOfWork,
    ProjectStatusTransitionEvaluator TransitionEvaluator,
    INotificationDispatcher? Notifications,
    ILogger<ProjectService>? Logger,
    IProjectChatService? ProjectChats,
    ISearchIndexService? Search,
    IProjectSearchIndexer? ProjectSearchIndexer);
