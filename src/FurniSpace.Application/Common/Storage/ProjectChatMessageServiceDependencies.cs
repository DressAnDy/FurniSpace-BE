using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.ProjectChatMessages;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Storage;

public sealed record ProjectChatMessageServiceDependencies(
    IProjectChatRealtimeService Realtime,
    IUnitOfWork UnitOfWork,
    ProjectChatFileUploadDependencies FileUpload,
    ILogger<ProjectChatMessageServiceDependencies> Logger,
    ISearchIndexService? Search,
    IChatMessageSearchIndexer? ChatMessageSearchIndexer);
