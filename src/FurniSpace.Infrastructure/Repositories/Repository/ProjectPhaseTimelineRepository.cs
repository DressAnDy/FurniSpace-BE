using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectPhaseTimelineRepository
    : GenericRepository<ProjectPhaseTimeline>, IProjectPhaseTimelineRepository
{
    public ProjectPhaseTimelineRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<ProjectPhaseTimeline>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectPhaseTimelineSet
            .Where(timeline => timeline.ProjectId == projectId)
            .OrderBy(timeline => timeline.DueDate)
            .ThenBy(timeline => timeline.Phase)
            .ToListAsync(cancellationToken);
    }

    public Task<ProjectPhaseTimeline?> GetByProjectAndPhaseAsync(
        Guid projectId,
        ProjectPhaseType phase,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectPhaseTimelineSet
            .FirstOrDefaultAsync(
                timeline => timeline.ProjectId == projectId && timeline.Phase == phase,
                cancellationToken);
    }
}
