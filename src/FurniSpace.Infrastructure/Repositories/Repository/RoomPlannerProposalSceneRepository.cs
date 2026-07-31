using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.RoomPlanner;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class RoomPlannerProposalSceneRepository : IRoomPlannerProposalSceneRepository
{
    private readonly AppDbContext _dbContext;

    public RoomPlannerProposalSceneRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomPlannerSceneContextReadModel?> GetContextAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        var context = await _dbContext.ProposalSceneSet
            .Where(scene => scene.SceneId == sceneId)
            .Join(
                _dbContext.ProposalSet,
                scene => scene.ProposalId,
                proposal => proposal.ProposalId,
                (scene, proposal) => new { scene, proposal })
            .Join(
                _dbContext.ProjectSet,
                joined => joined.proposal.ProjectId,
                project => project.ProjectId,
                (joined, project) => new RoomPlannerSceneContextReadModel
                {
                    SceneId = joined.scene.SceneId,
                    ProposalId = joined.scene.ProposalId,
                    ProjectId = project.ProjectId,
                    MongoSceneId = joined.scene.MongoSceneId,
                    ProposalStatus = joined.proposal.Status,
                    PublishedAt = joined.proposal.PublishedAt,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            return null;
        }

        context.SceneAreas = await GetSceneAreasAsync(sceneId, cancellationToken);
        return context;
    }

    public async Task UpdateMongoSceneIdAsync(
        Guid sceneId,
        string mongoSceneId,
        CancellationToken cancellationToken = default)
    {
        var scene = await _dbContext.ProposalSceneSet
            .FirstOrDefaultAsync(proposalScene => proposalScene.SceneId == sceneId, cancellationToken)
            .ConfigureAwait(false);

        if (scene is null)
        {
            return;
        }

        scene.MongoSceneId = mongoSceneId;
        scene.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<List<ProposalSceneAreaReadModel>> GetSceneAreasAsync(
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ProposalSceneAreaSet
            .Where(area => area.SceneId == sceneId)
            .Join(
                _dbContext.ProjectAreaSet,
                area => area.ProjectAreaId,
                projectArea => projectArea.ProjectAreaId,
                (area, projectArea) => new ProposalSceneAreaReadModel
                {
                    ProposalSceneAreaId = area.ProposalSceneAreaId,
                    SceneId = area.SceneId,
                    ProjectAreaId = area.ProjectAreaId,
                    AreaName = projectArea.AreaName,
                    FloorNumber = projectArea.FloorNumber,
                    SortOrder = area.SortOrder
                })
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.FloorNumber)
            .ThenBy(area => area.ProjectAreaId)
            .ToListAsync(cancellationToken);
    }
}
