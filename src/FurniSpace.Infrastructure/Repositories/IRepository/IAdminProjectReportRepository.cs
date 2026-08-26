#nullable enable

using FurniSpace.Infrastructure.ReadModels.Reports;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IAdminProjectReportRepository
{
    Task<IReadOnlyList<AdminProjectReportCandidateReadModel>> GetCandidatesAsync(
        AdminProjectReportListQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<AdminProjectReportCandidateReadModel?> GetCandidateAsync(
        Guid projectId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
