using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectShowcaseRepository
{
    Task AddAsync(ProjectShowcase showcase, CancellationToken cancellationToken = default);

    Task<ProjectShowcase?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectShowcase?> GetByIdAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<ProjectShowcase?> GetForUpdateAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<ProjectShowcaseDetailReadModel?> GetDetailAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<bool> ProjectHasShowcaseAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, Guid? excludeShowcaseId = null, CancellationToken cancellationToken = default);

    Task AddMediaAsync(ProjectShowcaseMedia media, CancellationToken cancellationToken = default);

    Task<List<ProjectShowcaseMedia>> GetMediaForUpdateAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<ProjectShowcaseMedia?> GetMediaForUpdateAsync(Guid showcaseId, Guid mediaId, CancellationToken cancellationToken = default);

    Task RemoveMediaAsync(ProjectShowcaseMedia media, CancellationToken cancellationToken = default);

    Task<int> GetNextMediaDisplayOrderAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<bool> HasCoverMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<int> CountMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<bool> HasInactiveMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default);

    Task<List<PublicShowcaseListItemReadModel>> GetPublishedPagedAsync(
        ProjectShowcaseListQueryReadModel query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountPublishedAsync(
        ProjectShowcaseListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<PublicShowcaseDetailReadModel?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<AdminProjectShowcaseListItemReadModel>> GetAdminPagedAsync(
        ProjectShowcaseListQueryReadModel query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAdminAsync(
        ProjectShowcaseListQueryReadModel query,
        CancellationToken cancellationToken = default);
}
