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

    new Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default);

    Task<OrderItem?> GetItemByIdAsync(
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OrderItem?>(null);
    }

    Task<OrderAdjustment?> GetAdjustmentByIdAsync(
        Guid orderAdjustmentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OrderAdjustment?>(null);
    }

    Task<OrderAdjustmentItem?> GetAdjustmentItemByIdAsync(
        Guid orderAdjustmentItemId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OrderAdjustmentItem?>(null);
    }

    Task<IReadOnlyList<OrderAdjustmentItem>> GetAdjustmentItemsAsync(
        Guid orderAdjustmentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OrderAdjustmentItem>>([]);
    }

    Task<bool> HasCancelledProductionItemAsync(
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task AddAdjustmentAsync(
        OrderAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    Task AddAdjustmentItemAsync(
        OrderAdjustmentItem item,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    void UpdateAdjustment(OrderAdjustment adjustment)
    {
    }

    void UpdateAdjustmentItem(OrderAdjustmentItem item)
    {
    }

    void RemoveAdjustmentItem(OrderAdjustmentItem item)
    {
    }

    new void Update(Order order);
}
