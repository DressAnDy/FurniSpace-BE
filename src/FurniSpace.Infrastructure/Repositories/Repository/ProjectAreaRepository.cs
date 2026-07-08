using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProjectAreas;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectAreaRepository : GenericRepository<ProjectArea>, IProjectAreaRepository
{
    public ProjectAreaRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<ProjectAreaDetailReadModel?> GetDetailAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        return BuildDetailQuery()
            .Where(area => area.ProjectAreaId == projectAreaId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectAreaDetailReadModel>> GetListByProjectAsync(
        Guid projectId,
        bool includeCancelled,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDetailQuery()
            .Where(area => area.ProjectId == projectId);

        if (!includeCancelled)
        {
            query = query.Where(area => area.Status != ProjectAreaStatus.CANCELLED);
        }

        return await query
            .OrderBy(area => area.AreaName)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> BelongsToProjectAsync(
        Guid projectAreaId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectAreaSet.AnyAsync(
            area => area.ProjectAreaId == projectAreaId && area.ProjectId == projectId,
            cancellationToken);
    }

    public async Task<bool> HasActiveUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        var hasActiveScene = await DbContext.ProposalSceneSet.AnyAsync(
            scene => scene.ProjectAreaId == projectAreaId && scene.IsActive == true,
            cancellationToken);

        if (hasActiveScene)
        {
            return true;
        }

        return await DbContext.ProposalItemSet.AnyAsync(
            item => item.ProjectAreaId == projectAreaId,
            cancellationToken);
    }

    private IQueryable<ProjectAreaDetailReadModel> BuildDetailQuery()
    {
        return DbContext.ProjectAreaSet
            .Join(
                DbContext.ProjectSet,
                area => area.ProjectId,
                project => project.ProjectId,
                (area, project) => new ProjectAreaDetailReadModel
                {
                    ProjectAreaId = area.ProjectAreaId,
                    ProjectId = area.ProjectId,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId,
                    ParentAreaId = area.ParentAreaId,
                    AreaName = area.AreaName,
                    AreaType = area.AreaType,
                    FloorNumber = area.FloorNumber,
                    Description = area.Description,
                    AreaSqm = area.AreaSqm,
                    Width = area.Width,
                    Length = area.Length,
                    Height = area.Height,
                    CurrentCondition = area.CurrentCondition,
                    RequirementNote = area.RequirementNote,
                    Status = area.Status,
                    CreatedBy = area.CreatedBy,
                    CreatedAt = area.CreatedAt,
                    UpdatedAt = area.UpdatedAt
                });
    }
}
