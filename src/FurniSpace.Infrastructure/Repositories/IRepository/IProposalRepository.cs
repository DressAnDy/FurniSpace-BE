using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Proposals;
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

    Task AddSceneAsync(
        ProposalScene scene,
        CancellationToken cancellationToken = default);

    Task<ProposalDetailReadModel?> GetDetailAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);
}
