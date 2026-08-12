using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.Proposals;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProposalRepository : GenericRepository<Proposal>, IProposalRepository
{
    private static readonly ProposalStatus[] CustomerVisibleStatuses =
    [
        ProposalStatus.PUBLISHED,
        ProposalStatus.REVISION_REQUESTED,
        ProposalStatus.SELECTED,
        ProposalStatus.REJECTED
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

    public Task<int> CountScenesAsync(
        ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildSceneListQuery(query).CountAsync(cancellationToken);
    }

    public Task AddSceneAsync(
        ProposalScene scene,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet.AddAsync(scene, cancellationToken).AsTask();
    }

    public async Task<List<ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(
        List<Guid> projectAreaIds,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaIds.Count == 0)
        {
            return [];
        }

        return await DbContext.ProjectAreaSet
            .Where(area => projectAreaIds.Contains(area.ProjectAreaId))
            .Select(area => new ProposalProjectAreaReadModel
            {
                ProjectAreaId = area.ProjectAreaId,
                ProjectId = area.ProjectId,
                AreaName = area.AreaName,
                AreaType = area.AreaType,
                FloorNumber = area.FloorNumber,
                Status = area.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceSceneAreasAsync(
        Guid sceneId,
        List<Guid> projectAreaIds,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existingAreas = await DbContext.ProposalSceneAreaSet
            .Where(area => area.SceneId == sceneId)
            .ToListAsync(cancellationToken);

        DbContext.ProposalSceneAreaSet.RemoveRange(existingAreas);

        for (var index = 0; index < projectAreaIds.Count; index++)
        {
            DbContext.ProposalSceneAreaSet.Add(new ProposalSceneArea
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = sceneId,
                ProjectAreaId = projectAreaIds[index],
                SortOrder = index,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
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

    public async Task<ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await DbContext.ProposalSet
            .Where(item =>
                item.ProjectId == projectId &&
                (item.Status == ProposalStatus.PUBLISHED))
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.VersionNo)
            .ThenByDescending(item => item.CreatedAt)
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

        proposal.Scenes = await GetScenesAsync(proposal.ProposalId, cancellationToken);
        proposal.Items = await GetItemsAsync(proposal.ProposalId, cancellationToken);
        return proposal;
    }

    public async Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(
        ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var scenes = await BuildSceneListQuery(query)
            .OrderBy(scene => scene.VersionNo)
            .ThenBy(scene => scene.CreatedAt)
            .ThenBy(scene => scene.SceneId)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
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
            .ToListAsync(cancellationToken);

        await AttachSceneAreasAsync(scenes, cancellationToken);
        return scenes;
    }

    public async Task<ProposalSceneDetailReadModel?> GetSceneDetailAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        var scene = await BuildSceneDetailQuery()
            .Where(scene => scene.SceneId == sceneId)
            .FirstOrDefaultAsync(cancellationToken);
        if (scene is null)
        {
            return null;
        }

        await AttachSceneAreasAsync([scene], cancellationToken);
        return scene;
    }

    public async Task<ProposalSceneContextReadModel?> GetSceneContextAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildSceneContextQuery()
            .Where(scene => scene.ProposalId == proposalId && scene.SceneId == sceneId)
            .FirstOrDefaultAsync(cancellationToken);
        return await AttachSceneAreasAsync(context, cancellationToken);
    }

    public async Task<ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildSceneContextQuery()
            .Where(scene => scene.SceneId == sceneId)
            .FirstOrDefaultAsync(cancellationToken);
        return await AttachSceneAreasAsync(context, cancellationToken);
    }

    public Task<ProposalScene?> GetSceneEntityAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSceneSet
            .Include(scene => scene.SceneAreas)
            .FirstOrDefaultAsync(scene => scene.SceneId == sceneId, cancellationToken);
    }

    private IQueryable<ProposalScene> BuildSceneListQuery(ProposalSceneListQueryReadModel query)
    {
        var scenes = DbContext.ProposalSceneSet.Where(scene => scene.ProposalId == query.ProposalId);

        if (query.ActiveOnly)
        {
            scenes = scenes.Where(scene => scene.IsActive == true);
        }
        else if (query.IsActive.HasValue)
        {
            scenes = scenes.Where(scene => scene.IsActive == query.IsActive);
        }

        if (query.SceneType.HasValue)
        {
            scenes = scenes.Where(scene => scene.SceneType == query.SceneType);
        }

        return scenes;
    }

    private IQueryable<ProposalSceneDetailReadModel> BuildSceneDetailQuery()
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
                (joined, project) => new { joined.scene, joined.proposal, project })
            .GroupJoin(
                DbContext.StoredFileSet,
                joined => joined.scene.PreviewFileId,
                file => file.FileId,
                (joined, files) => new { joined.scene, joined.proposal, joined.project, files })
            .SelectMany(
                joined => joined.files.DefaultIfEmpty(),
                (joined, file) => new ProposalSceneDetailReadModel
                {
                    SceneId = joined.scene.SceneId,
                    ProposalId = joined.scene.ProposalId,
                    ProjectId = joined.proposal.ProjectId,
                    CustomerId = joined.project.CustomerId,
                    AssignedSalesId = joined.project.AssignedSalesId,
                    AssignedDesignerId = joined.project.AssignedDesignerId,
                    ProposalStatus = joined.proposal.Status,
                    SceneName = joined.scene.SceneName,
                    SceneType = joined.scene.SceneType,
                    MongoSceneId = joined.scene.MongoSceneId,
                    PreviewFileId = joined.scene.PreviewFileId,
                    PreviewFileUrl = file == null ? null : file.FileUrl,
                    VersionNo = joined.scene.VersionNo,
                    IsActive = joined.scene.IsActive,
                    CreatedAt = joined.scene.CreatedAt,
                    UpdatedAt = joined.scene.UpdatedAt
                });
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
                    SceneType = joined.scene.SceneType,
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

    public async Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(
        ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildItemListQuery(query)
            .OrderBy(item => item.ItemName)
            .ThenBy(item => item.ProposalItemId)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .GroupJoin(
                DbContext.ProductVersionSet,
                item => item.ProductVersionId,
                version => version.ProductVersionId,
                (item, versions) => new { item, versions })
            .SelectMany(
                joined => joined.versions.DefaultIfEmpty(),
                (joined, version) => new { joined.item, version })
            .GroupJoin(
                DbContext.ProjectAreaSet,
                joined => joined.item.ProjectAreaId,
                area => area.ProjectAreaId,
                (joined, areas) => new { joined.item, joined.version, areas })
            .SelectMany(
                joined => joined.areas.DefaultIfEmpty(),
                (joined, area) => new ProposalItemReadModel
                {
                    ProposalItemId = joined.item.ProposalItemId,
                    ProposalId = joined.item.ProposalId,
                    SceneId = joined.item.SceneId,
                    SceneObjectId = joined.item.SceneObjectId,
                    ProjectAreaId = joined.item.ProjectAreaId,
                    ProjectAreaName = area == null ? null : area.AreaName,
                    FloorNumber = area == null ? null : area.FloorNumber,
                    ProductVersionId = joined.item.ProductVersionId,
                    ProductNameSnapshot = joined.item.ItemName,
                    VersionNameSnapshot = joined.version == null ? null : joined.version.VersionName,
                    MaterialSnapshot = joined.item.Material,
                    ColorSnapshot = joined.item.Color,
                    WidthSnapshot = joined.item.Width,
                    HeightSnapshot = joined.item.Height,
                    DepthSnapshot = joined.item.Depth,
                    DimensionUnit = joined.version == null ? null : joined.version.DimensionUnit,
                    Quantity = joined.item.Quantity,
                    UnitPriceSnapshot = joined.item.UnitPriceSnapshot,
                    SubtotalAmount = joined.item.TotalPriceSnapshot,
                    CustomizationNote = joined.item.Note
                })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountItemsAsync(
        ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildItemListQuery(query).CountAsync(cancellationToken);
    }

    public Task<ProposalItemDetailReadModel?> GetItemDetailAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet
            .Where(item => item.ProposalItemId == proposalItemId)
            .Join(
                DbContext.ProposalSet,
                item => item.ProposalId,
                proposal => proposal.ProposalId,
                (item, proposal) => new { item, proposal })
            .Join(
                DbContext.ProjectSet,
                joined => joined.proposal.ProjectId,
                project => project.ProjectId,
                (joined, project) => new { joined.item, joined.proposal, project })
            .GroupJoin(
                DbContext.ProductVersionSet,
                joined => joined.item.ProductVersionId,
                version => version.ProductVersionId,
                (joined, versions) => new { joined, versions })
            .SelectMany(
                grouped => grouped.versions.DefaultIfEmpty(),
                (grouped, version) => new { grouped.joined, version })
            .GroupJoin(
                DbContext.ProjectAreaSet,
                joined => joined.joined.item.ProjectAreaId,
                area => area.ProjectAreaId,
                (joined, areas) => new { joined.joined, joined.version, areas })
            .SelectMany(
                grouped => grouped.areas.DefaultIfEmpty(),
                (grouped, area) => new ProposalItemDetailReadModel
                {
                    ProposalItemId = grouped.joined.item.ProposalItemId,
                    ProposalId = grouped.joined.item.ProposalId,
                    SceneId = grouped.joined.item.SceneId,
                    SceneObjectId = grouped.joined.item.SceneObjectId,
                    ProjectAreaId = grouped.joined.item.ProjectAreaId,
                    ProjectAreaName = area == null ? null : area.AreaName,
                    FloorNumber = area == null ? null : area.FloorNumber,
                    ProductVersionId = grouped.joined.item.ProductVersionId,
                    ProductNameSnapshot = grouped.joined.item.ItemName,
                    VersionNameSnapshot = grouped.version == null ? null : grouped.version.VersionName,
                    MaterialSnapshot = grouped.joined.item.Material,
                    ColorSnapshot = grouped.joined.item.Color,
                    WidthSnapshot = grouped.joined.item.Width,
                    HeightSnapshot = grouped.joined.item.Height,
                    DepthSnapshot = grouped.joined.item.Depth,
                    DimensionUnit = grouped.version == null ? null : grouped.version.DimensionUnit,
                    Quantity = grouped.joined.item.Quantity,
                    UnitPriceSnapshot = grouped.joined.item.UnitPriceSnapshot,
                    SubtotalAmount = grouped.joined.item.TotalPriceSnapshot,
                    CustomizationNote = grouped.joined.item.Note,
                    UpdatedAt = grouped.joined.item.UpdatedAt,
                    ProjectId = grouped.joined.proposal.ProjectId,
                    CustomerId = grouped.joined.project.CustomerId,
                    AssignedSalesId = grouped.joined.project.AssignedSalesId,
                    AssignedDesignerId = grouped.joined.project.AssignedDesignerId,
                    ProposalStatus = grouped.joined.proposal.Status
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProposalItem?> GetItemEntityAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet
            .FirstOrDefaultAsync(item => item.ProposalItemId == proposalItemId, cancellationToken);
    }

    public Task AddItemAsync(
        ProposalItem item,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public void RemoveItem(ProposalItem item)
    {
        DbContext.ProposalItemSet.Remove(item);
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

    public Task<Proposal?> GetSelectedProposalByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalSet
            .Where(proposal =>
                proposal.ProjectId == projectId &&
                proposal.Status == ProposalStatus.SELECTED)
            .OrderByDescending(proposal => proposal.SelectedAt)
            .ThenByDescending(proposal => proposal.ProposalId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> RestoreAutoRejectedProposalsAsync(
        Guid projectId,
        DateTime autoRejectedAt,
        DateTime restoredAt,
        CancellationToken cancellationToken = default)
    {
        var proposals = await DbContext.ProposalSet
            .Where(proposal =>
                proposal.ProjectId == projectId &&
                proposal.Status == ProposalStatus.REJECTED &&
                proposal.RejectedAt == autoRejectedAt)
            .ToListAsync(cancellationToken);

        foreach (var proposal in proposals)
        {
            proposal.Status = ProposalStatus.PUBLISHED;
            proposal.RejectedAt = null;
            proposal.UpdatedAt = restoredAt;
        }

        return proposals.Count;
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

    private IQueryable<ProposalItem> BuildItemListQuery(ProposalItemListQueryReadModel query)
    {
        var items = DbContext.ProposalItemSet.Where(item => item.ProposalId == query.ProposalId);

        if (query.SceneId.HasValue)
        {
            items = items.Where(item => item.SceneId == query.SceneId.Value);
        }

        return items;
    }

    private async Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var scenes = await DbContext.ProposalSceneSet
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

        await AttachSceneAreasAsync(scenes, cancellationToken);
        return scenes;
    }

    private async Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        return await DbContext.ProposalItemSet
            .Where(item => item.ProposalId == proposalId)
            .GroupJoin(
                DbContext.ProjectAreaSet,
                item => item.ProjectAreaId,
                area => area.ProjectAreaId,
                (item, areas) => new { item, areas })
            .SelectMany(
                joined => joined.areas.DefaultIfEmpty(),
                (joined, area) => new ProposalItemReadModel
                {
                    ProposalItemId = joined.item.ProposalItemId,
                    ProposalId = joined.item.ProposalId,
                    SceneId = joined.item.SceneId,
                    SceneObjectId = joined.item.SceneObjectId,
                    ProjectAreaId = joined.item.ProjectAreaId,
                    ProjectAreaName = area == null ? null : area.AreaName,
                    FloorNumber = area == null ? null : area.FloorNumber,
                    ProductVersionId = joined.item.ProductVersionId,
                    ProductNameSnapshot = joined.item.ItemName,
                    VersionNameSnapshot = null,
                    Quantity = joined.item.Quantity,
                    UnitPriceSnapshot = joined.item.UnitPriceSnapshot,
                    SubtotalAmount = joined.item.TotalPriceSnapshot
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

    private async Task AttachSceneAreasAsync(
        IReadOnlyCollection<ProposalSceneBaseDto<ProposalSceneType?>> scenes,
        CancellationToken cancellationToken)
    {
        var areaLookup = await GetSceneAreaLookupAsync(
            scenes.Select(scene => scene.SceneId).ToList(),
            cancellationToken);

        foreach (var scene in scenes)
        {
            AssignSceneAreas(scene, areaLookup);
        }
    }

    private async Task<ProposalSceneContextReadModel?> AttachSceneAreasAsync(
        ProposalSceneContextReadModel? context,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return null;
        }

        var areaLookup = await GetSceneAreaLookupAsync([context.SceneId], cancellationToken);
        context.SceneAreas = areaLookup.GetValueOrDefault(context.SceneId) ?? [];
        return context;
    }

    private async Task<Dictionary<Guid, List<ProposalSceneAreaReadModel>>> GetSceneAreaLookupAsync(
        List<Guid> sceneIds,
        CancellationToken cancellationToken)
    {
        if (sceneIds.Count == 0)
        {
            return [];
        }

        var areas = await DbContext.ProposalSceneAreaSet
            .Where(area => sceneIds.Contains(area.SceneId))
            .Join(
                DbContext.ProjectAreaSet,
                area => area.ProjectAreaId,
                projectArea => projectArea.ProjectAreaId,
                (area, projectArea) => new ProposalSceneAreaReadModel
                {
                    ProposalSceneAreaId = area.ProposalSceneAreaId,
                    SceneId = area.SceneId,
                    ProjectAreaId = area.ProjectAreaId,
                    AreaName = projectArea.AreaName,
                    AreaType = projectArea.AreaType,
                    FloorNumber = projectArea.FloorNumber,
                    Status = projectArea.Status,
                    SortOrder = area.SortOrder
                })
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.FloorNumber)
            .ThenBy(area => area.ProjectAreaId)
            .ToListAsync(cancellationToken);

        return areas
            .GroupBy(area => area.SceneId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private static void AssignSceneAreas(
        ProposalSceneBaseDto<ProposalSceneType?> scene,
        IReadOnlyDictionary<Guid, List<ProposalSceneAreaReadModel>> areaLookup)
    {
        var areas = areaLookup.GetValueOrDefault(scene.SceneId) ?? [];
        scene.Areas = areas
            .Select(area => new ProposalSceneAreaDto
            {
                ProjectAreaId = area.ProjectAreaId,
                AreaName = area.AreaName,
                AreaType = area.AreaType?.ToString(),
                FloorNumber = area.FloorNumber,
                SortOrder = area.SortOrder,
                Status = area.Status?.ToString()
            })
            .ToList();
    }

}
