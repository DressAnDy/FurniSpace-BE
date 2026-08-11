#nullable enable

using FurniSpace.Infrastructure.ReadModels.Projects;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectWorkflowRepository
{
    Task<ProjectWorkflowSnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
