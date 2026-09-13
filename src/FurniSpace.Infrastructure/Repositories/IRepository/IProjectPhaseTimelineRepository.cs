using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectPhaseTimelineRepository : IGenericRepository<ProjectPhaseTimeline>
{
    Task<List<ProjectPhaseTimeline>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseTimeline?> GetByProjectAndPhaseAsync(
        Guid projectId,
        ProjectPhaseType phase,
        CancellationToken cancellationToken = default);
}
