using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface ICustomizationRequestRepository : IGenericRepository<CustomizationRequest>
{
    Task<IReadOnlyList<CustomizationRequestReadModel>> GetByProjectAsync(
        CustomizationRequestQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<CustomizationRequestDetailReadModel?> GetDetailAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default);

    Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default);

    Task<bool> HasQuotationForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<bool> HasProductionVisibleRequestAsync(
        Guid projectId,
        Guid productionUserId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPendingForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveRequestForProposalItemAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>> GetProductionQueueAsync(
        ProductionCustomizationRequestQueueQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountProductionQueueAsync(
        ProductionCustomizationRequestQueueQueryReadModel query,
        CancellationToken cancellationToken = default);
}
