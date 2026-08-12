using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProposalRepository : IGenericRepository<Proposal>
{
    Task<ProposalProjectAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProposalContextReadModel?> GetProposalContextAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<int> CountByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalReadModel>> GetListAsync(
        ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountListAsync(
        ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountScenesAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<int> CountScenesAsync(
        ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task AddSceneAsync(
        ProposalScene scene,
        CancellationToken cancellationToken = default);

    Task<List<ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(
        List<Guid> projectAreaIds,
        CancellationToken cancellationToken = default);

    Task ReplaceSceneAreasAsync(
        Guid sceneId,
        List<Guid> projectAreaIds,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task<ProposalDetailReadModel?> GetDetailAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(
        ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProposalSceneDetailReadModel?> GetSceneDetailAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task<ProposalSceneContextReadModel?> GetSceneContextAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task<ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task<ProposalScene?> GetSceneEntityAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(
        ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountItemsAsync(
        ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProposalItemDetailReadModel?> GetItemDetailAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default);

    Task<ProposalItem?> GetItemEntityAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default);

    Task AddItemAsync(
        ProposalItem item,
        CancellationToken cancellationToken = default);

    void RemoveItem(ProposalItem item);

    Task<Proposal?> GetProposalEntityAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task RejectOtherActiveProposalsAsync(
        Guid projectId,
        Guid selectedProposalId,
        DateTime rejectedAt,
        CancellationToken cancellationToken = default);

    Task<Proposal?> GetSelectedProposalByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Proposal?>(null);
    }

    Task<int> RestoreAutoRejectedProposalsAsync(
        Guid projectId,
        DateTime autoRejectedAt,
        DateTime restoredAt,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    Task<bool> HasProposalWithActiveSceneAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> HasSelectedFinalProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveSceneAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<bool> ProjectAreaBelongsToProjectAsync(
        Guid projectAreaId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
