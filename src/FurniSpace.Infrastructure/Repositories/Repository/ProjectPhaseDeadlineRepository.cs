using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectPhaseDeadlineRepository
    : GenericRepository<ProjectPhaseDeadline>, IProjectPhaseDeadlineRepository
{
    public ProjectPhaseDeadlineRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<ProjectPhaseDeadline>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectPhaseDeadlineSet
            .Where(deadline => deadline.ProjectId == projectId)
            .OrderBy(deadline => deadline.DueDate)
            .ThenBy(deadline => deadline.Phase)
            .ToListAsync(cancellationToken);
    }

    public Task<ProjectPhaseDeadline?> GetByProjectAndPhaseAsync(
        Guid projectId,
        ProjectPhaseType phase,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectPhaseDeadlineSet
            .FirstOrDefaultAsync(
                deadline => deadline.ProjectId == projectId && deadline.Phase == phase,
                cancellationToken);
    }
}
