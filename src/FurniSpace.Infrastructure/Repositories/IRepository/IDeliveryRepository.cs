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

    Task<Delivery?> GetByProjectScheduleIdAsync(
        Guid projectScheduleId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Delivery?>(null);
    }

    Task<bool> ExistsByProjectScheduleIdAsync(
        Guid projectScheduleId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<bool> HasInProgressDeliveryAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<OrderDeliveryTrackingReadModel?> GetTrackingByOrderAsync(
        Guid orderId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OrderDeliveryTrackingReadModel?>(null);
    }

    Task<ProjectDeliverySummaryReadModel?> GetProjectDeliverySummaryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProjectDeliverySummaryReadModel?>(null);
    }

    void Update(Delivery delivery);
}
