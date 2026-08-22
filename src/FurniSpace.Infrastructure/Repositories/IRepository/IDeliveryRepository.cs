using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IDeliveryRepository
{
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default);

    Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default);

    Task<DeliveryDetailReadModel?> GetDetailAsync(
        Guid orderId,
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Delivery?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    void Update(Delivery delivery);
}
