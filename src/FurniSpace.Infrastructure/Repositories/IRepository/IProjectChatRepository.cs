using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectChats;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectChatRepository : IGenericRepository<ProjectChat>
{
    Task<ProjectChatAccessReadModel?> GetAccessAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ProjectChatStatusAccessReadModel?> GetStatusAccessAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProjectChatListItemReadModel> Items, int Total)> GetListAsync(
        Guid projectId,
        ProjectChatListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProjectChat?> GetActiveAsync(
        Guid projectId,
        ProjectChatType chatType,
        CancellationToken cancellationToken = default);
}
