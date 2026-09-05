using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IOperationalDelayReportRepository : IGenericRepository<OperationalDelayReport>
{
    Task<OperationalDelayReportDetailReadModel?> GetDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationalDelayReportListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        OperationalDelayPhase phase,
        CancellationToken cancellationToken = default);
}
