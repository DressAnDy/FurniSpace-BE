using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectPhaseDeadlineRepository : IGenericRepository<ProjectPhaseDeadline>
{
    Task<List<ProjectPhaseDeadline>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseDeadline?> GetByProjectAndPhaseAsync(
        Guid projectId,
        ProjectPhaseType phase,
        CancellationToken cancellationToken = default);
}
