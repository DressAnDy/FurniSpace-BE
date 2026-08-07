using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.ProjectAreas;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectAreaRepository : IGenericRepository<ProjectArea>
{
    Task<ProjectAreaDetailReadModel?> GetDetailAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectAreaDetailReadModel>> GetListByProjectAsync(
        Guid projectId,
        bool includeCancelled,
        CancellationToken cancellationToken = default);

    Task<bool> BelongsToProjectAsync(
        Guid projectAreaId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveSceneUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveProposalItemUsageAsync(
        Guid projectAreaId,
        CancellationToken cancellationToken = default);
}
