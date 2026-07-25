#nullable enable

using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProductionRequestRepository : IGenericRepository<ProductionRequest>
{
    Task<bool> HasActiveRequestForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<int> CountCreatedOnAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<List<OrderItem>> GetProductOrderItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task AddItemsAsync(
        List<ProductionItem> items,
        CancellationToken cancellationToken = default);

    Task<bool> IsActiveProductionStaffAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<ProductionAssigneeReadModel?> GetAssigneeAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<List<AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<List<ProductionRequestListItemReadModel>> GetQueueAsync(
        ProductionRequestQueueReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProductionRequestDetailReadModel?> GetDetailAsync(
        Guid productionRequestId,
        CancellationToken cancellationToken = default);

    Task<ProductionItem?> GetItemByIdAsync(
        Guid productionItemId,
        CancellationToken cancellationToken = default);

    Task<List<ProductionItem>> GetItemsByRequestIdAsync(
        Guid productionRequestId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<ProductionItem>());
    }

    Task<ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(
        Guid productionItemId,
        CancellationToken cancellationToken = default);

    void UpdateItem(ProductionItem item);
}
