using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.ProductIssues;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IDeliveryProductIssueReportRepository : IGenericRepository<DeliveryProductIssueReport>
{
    Task<DeliveryProductIssueReportDetailReadModel?> GetDetailAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
