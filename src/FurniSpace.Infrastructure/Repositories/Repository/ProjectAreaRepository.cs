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

    public Task<bool> ActiveFloorNumberExistsAsync(
        Guid projectId,
        int floorNumber,
        Guid? excludedProjectAreaId = null,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectAreaSet.AnyAsync(
            area =>
                area.ProjectId == projectId &&
                area.AreaType == ProjectAreaType.FLOOR &&
                area.Status != ProjectAreaStatus.CANCELLED &&
                area.FloorNumber == floorNumber &&
                (!excludedProjectAreaId.HasValue || area.ProjectAreaId != excludedProjectAreaId.Value),
            cancellationToken);
    }

    public async Task<bool> HasActiveUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        return await HasActiveSceneUsageAsync(projectAreaId, cancellationToken) ||
               await HasActiveProposalItemUsageAsync(projectAreaId, cancellationToken);
    }

    public Task<bool> HasActiveSceneUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneAreaSet
            .Where(mapping => mapping.ProjectAreaId == projectAreaId)
            .Join(
                DbContext.ProposalSceneSet.Where(scene => scene.IsActive == true),
                mapping => mapping.SceneId,
                scene => scene.SceneId,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> HasActiveProposalItemUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet.AnyAsync(
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
                    IsSpecialLayout = area.IsSpecialLayout,
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
