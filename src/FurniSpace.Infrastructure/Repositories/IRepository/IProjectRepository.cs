using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectRepository : IGenericRepository<Project>
{
    Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<int> CountSubmittedInYearAsync(
        int year,
        CancellationToken cancellationToken = default);

    Task<ProjectDetailReadModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default);
}
