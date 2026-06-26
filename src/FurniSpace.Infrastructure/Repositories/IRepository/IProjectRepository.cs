using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectRepository : IGenericRepository<Project>
{
    Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<string?> GetAccountFullNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default);

    Task<int> CountSubmittedInYearAsync(
        int year,
        CancellationToken cancellationToken = default);

    Task<ProjectDetailReadModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
        Guid designerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
        ProjectByUserQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountByUserAsync(
        ProjectByUserQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);
}
