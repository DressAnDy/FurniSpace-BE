using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.RoomPlanner;
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

    public Task<RoomPlannerSceneContextReadModel?> GetContextAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProposalSceneSet
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
                    ProjectAreaId = joined.scene.ProjectAreaId,
                    MongoSceneId = joined.scene.MongoSceneId,
                    ProposalStatus = joined.proposal.Status,
                    PublishedAt = joined.proposal.PublishedAt,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                })
            .FirstOrDefaultAsync(cancellationToken);
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
}
