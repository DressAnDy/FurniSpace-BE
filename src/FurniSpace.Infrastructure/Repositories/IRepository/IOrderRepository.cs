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

    new void Update(Order order);
}
