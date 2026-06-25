using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectChatMessageRepository : IGenericRepository<ProjectChatMessage>
{
    Task<ProjectChatMessageAccessReadModel?> GetAccessAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProjectChatMessageReadModel> Items, int Total)> GetMessagesAsync(
        Guid chatId,
        ProjectChatMessageQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ChatMessageSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);
}
