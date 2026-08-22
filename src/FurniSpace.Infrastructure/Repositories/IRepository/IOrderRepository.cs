using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OrderDetailReadModel?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    new Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForQuotationAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<bool> HasProjectOrderInStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<OrderStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<Order?> GetLatestByProjectInStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<OrderStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Order?>(null);
    }

    new Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default);

    Task<OrderItem?> GetItemByIdAsync(
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OrderItem?>(null);
    }

    Task<IReadOnlyList<OrderItem>> GetItemsByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OrderItem>>([]);
    }

    void UpdateItem(OrderItem item)
    {
    }

    Task<bool> HasCompletedDeliveryFlowAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<bool> AllDeliverableItemsReadyAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<bool> AllDeliverableItemsDeliveredAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<int> GetTotalRemainingDeliverableQuantityAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    Task<IReadOnlyList<OrderItem>> GetItemsByIdsForUpdateAsync(
        IReadOnlyCollection<Guid> orderItemIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OrderItem>>([]);
    }

    new void Update(Order order);
}
