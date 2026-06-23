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
}
