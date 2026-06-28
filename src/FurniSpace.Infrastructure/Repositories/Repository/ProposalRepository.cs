using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProposalRepository : GenericRepository<Proposal>, IProposalRepository
{
    private static readonly ProposalStatus[] CustomerVisibleStatuses =
    [
        ProposalStatus.PUBLISHED,
        ProposalStatus.VIEWED,
        ProposalStatus.SELECTED,
        ProposalStatus.REVISION_REQUESTED
    ];

    public ProposalRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<ProposalProjectAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet
            .Where(project => project.ProjectId == projectId)
            .Select(project => new ProposalProjectAccessReadModel
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                ProjectStatus = project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProposalContextReadModel?> GetProposalContextAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet
            .Where(proposal => proposal.ProposalId == proposalId)
            .Join(
                DbContext.ProjectSet,
                proposal => proposal.ProjectId,
                project => project.ProjectId,
                (proposal, project) => new ProposalContextReadModel
                {
                    ProposalId = proposal.ProposalId,
                    ProjectId = project.ProjectId,
                    ProposalStatus = proposal.Status,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet.CountAsync(
            proposal => proposal.ProjectId == projectId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProposalReadModel>> GetListAsync(
        ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildListQuery(query)
            .OrderByDescending(proposal => proposal.VersionNo)
            .ThenByDescending(proposal => proposal.CreatedAt)
            .ThenByDescending(proposal => proposal.ProposalId)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(proposal => new ProposalReadModel
            {
                ProposalId = proposal.ProposalId,
                ProjectId = proposal.ProjectId,
                ParentProposalId = proposal.ParentProposalId,
                ProposalName = proposal.ProposalName,
                Description = proposal.Description,
                VersionNo = proposal.VersionNo,
                Status = proposal.Status,
                PublishedAt = proposal.PublishedAt,
                SelectedAt = proposal.SelectedAt,
                RejectedAt = proposal.RejectedAt,
                CreatedAt = proposal.CreatedAt,
                UpdatedAt = proposal.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountListAsync(
        ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildListQuery(query).CountAsync(cancellationToken);
    }

    public Task<int> CountScenesAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet.CountAsync(
            scene => scene.ProposalId == proposalId,
            cancellationToken);
    }

    public Task AddSceneAsync(
        ProposalScene scene,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet.AddAsync(scene, cancellationToken).AsTask();
    }

    public async Task<ProposalDetailReadModel?> GetDetailAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await DbContext.ProposalSet
            .Where(item => item.ProposalId == proposalId)
            .Join(
                DbContext.ProjectSet,
                item => item.ProjectId,
                project => project.ProjectId,
                (item, project) => new ProposalDetailReadModel
                {
                    ProposalId = item.ProposalId,
                    ProjectId = item.ProjectId,
                    ParentProposalId = item.ParentProposalId,
                    ProposalName = item.ProposalName,
                    Description = item.Description,
                    VersionNo = item.VersionNo,
                    Status = item.Status,
                    PublishedAt = item.PublishedAt,
                    SelectedAt = item.SelectedAt,
                    RejectedAt = item.RejectedAt,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (proposal is null)
        {
            return null;
        }

        proposal.Scenes = await GetScenesAsync(proposalId, cancellationToken);
        proposal.Items = await GetItemsAsync(proposalId, cancellationToken);
        return proposal;
    }

    public Task<ProposalSceneContextReadModel?> GetSceneContextAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return BuildSceneContextQuery()
            .Where(scene => scene.ProposalId == proposalId && scene.SceneId == sceneId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return BuildSceneContextQuery()
            .Where(scene => scene.SceneId == sceneId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProposalScene?> GetSceneEntityAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet
            .FirstOrDefaultAsync(scene => scene.SceneId == sceneId, cancellationToken);
    }

    private IQueryable<ProposalSceneContextReadModel> BuildSceneContextQuery()
    {
        return DbContext.ProposalSceneSet
            .Join(
                DbContext.ProposalSet,
                scene => scene.ProposalId,
                proposal => proposal.ProposalId,
                (scene, proposal) => new { scene, proposal })
            .Join(
                DbContext.ProjectSet,
                joined => joined.proposal.ProjectId,
                project => project.ProjectId,
                (joined, project) => new ProposalSceneContextReadModel
                {
                    SceneId = joined.scene.SceneId,
                    ProposalId = joined.proposal.ProposalId,
                    ProjectId = project.ProjectId,
                    ProjectAreaId = joined.scene.ProjectAreaId,
                    ProposalStatus = joined.proposal.Status,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                });
    }

    public async Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.ProposalItemSet
            .Where(item => item.ProposalId == proposalId && item.SceneId == sceneId)
            .ToListAsync(cancellationToken);
    }

    public Task AddItemAsync(
        ProposalItem item,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public Task<Proposal?> GetProposalEntityAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet
            .FirstOrDefaultAsync(proposal => proposal.ProposalId == proposalId, cancellationToken);
    }

    public async Task RejectOtherActiveProposalsAsync(
        Guid projectId,
        Guid selectedProposalId,
        DateTime rejectedAt,
        CancellationToken cancellationToken = default)
    {
        var proposals = await DbContext.ProposalSet
            .Where(proposal =>
                proposal.ProjectId == projectId &&
                proposal.ProposalId != selectedProposalId &&
                proposal.Status != ProposalStatus.ARCHIVED &&
                proposal.Status != ProposalStatus.REJECTED)
            .ToListAsync(cancellationToken);

        foreach (var proposal in proposals)
        {
            proposal.Status = ProposalStatus.REJECTED;
            proposal.RejectedAt = rejectedAt;
            proposal.UpdatedAt = rejectedAt;
        }
    }

    private IQueryable<Proposal> BuildListQuery(ProposalListQueryReadModel query)
    {
        var proposals = DbContext.ProposalSet.Where(proposal => proposal.ProjectId == query.ProjectId);

        if (query.Status.HasValue)
        {
            proposals = proposals.Where(proposal => proposal.Status == query.Status);
        }

        if (query.CustomerVisibleOnly)
        {
            proposals = proposals.Where(proposal =>
                proposal.Status.HasValue &&
                CustomerVisibleStatuses.Contains(proposal.Status.Value));
        }

        return proposals;
    }

    private async Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        return await DbContext.ProposalSceneSet
            .Where(scene => scene.ProposalId == proposalId && scene.IsActive != false)
            .GroupJoin(
                DbContext.StoredFileSet,
                scene => scene.PreviewFileId,
                file => file.FileId,
                (scene, files) => new { scene, files })
            .SelectMany(
                joined => joined.files.DefaultIfEmpty(),
                (joined, file) => new ProposalSceneReadModel
                {
                    SceneId = joined.scene.SceneId,
                    ProposalId = joined.scene.ProposalId,
                    ProjectAreaId = joined.scene.ProjectAreaId,
                    SceneName = joined.scene.SceneName,
                    SceneType = joined.scene.SceneType,
                    MongoSceneId = joined.scene.MongoSceneId,
                    PreviewFileId = joined.scene.PreviewFileId,
                    PreviewFileUrl = file == null ? null : file.FileUrl,
                    VersionNo = joined.scene.VersionNo,
                    IsActive = joined.scene.IsActive,
                    CreatedAt = joined.scene.CreatedAt,
                    UpdatedAt = joined.scene.UpdatedAt
                })
            .OrderBy(scene => scene.VersionNo)
            .ThenBy(scene => scene.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        return await DbContext.ProposalItemSet
            .Where(item => item.ProposalId == proposalId)
            .Select(item => new ProposalItemReadModel
            {
                ProposalItemId = item.ProposalItemId,
                SceneId = item.SceneId,
                SceneObjectId = null,
                ProductVersionId = item.ProductVersionId,
                ProductNameSnapshot = item.ItemName,
                VersionNameSnapshot = null,
                Quantity = item.Quantity,
                UnitPriceSnapshot = item.UnitPriceSnapshot,
                SubtotalAmount = item.TotalPriceSnapshot
            })
            .OrderBy(item => item.ProductNameSnapshot)
            .ThenBy(item => item.ProposalItemId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasProposalWithActiveSceneAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet
            .Where(proposal => proposal.ProjectId == projectId)
            .Join(
                DbContext.ProposalSceneSet.Where(scene => scene.IsActive == true),
                proposal => proposal.ProposalId,
                scene => scene.ProposalId,
                (proposal, _) => proposal.ProposalId)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> HasSelectedFinalProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet
            .AnyAsync(
                proposal =>
                    proposal.ProjectId == projectId &&
                    proposal.Status == ProposalStatus.SELECTED,
                cancellationToken);
    }

    public Task<bool> HasActiveSceneAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet.AnyAsync(
            scene => scene.ProposalId == proposalId && scene.IsActive == true,
            cancellationToken);
    }

    public Task<bool> FileExistsAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.StoredFileSet.AnyAsync(file => file.FileId == fileId, cancellationToken);
    }

    public Task<bool> ProjectAreaBelongsToProjectAsync(
        Guid projectAreaId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectAreaSet.AnyAsync(
            area => area.ProjectAreaId == projectAreaId && area.ProjectId == projectId,
            cancellationToken);
    }
}
