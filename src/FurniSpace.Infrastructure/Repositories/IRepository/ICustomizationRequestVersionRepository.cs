using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface ICustomizationRequestVersionRepository : IGenericRepository<CustomizationRequestVersion>
{
    Task<CustomizationRequestVersion?> GetByIdForUpdateAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default);

    Task<CustomizationRequestVersion?> GetByIdWithRequestAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNoAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomizationRequestVersionReadModel>> GetByRequestIdAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionCustomizationVersionQueueReadModel>> GetProductionQueueAsync(
        ProductionCustomizationVersionQueueQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountProductionQueueAsync(
        ProductionCustomizationVersionQueueQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProductionCustomizationVersionDetailReadModel?> GetProductionDetailAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkProductionReviewedAsync(
        ProductionVersionReviewUpdate update,
        CancellationToken cancellationToken = default);
}
