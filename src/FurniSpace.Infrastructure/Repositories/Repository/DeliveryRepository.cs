#nullable enable

using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly AppDbContext _dbContext;

    public DeliveryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.AddAsync(delivery, cancellationToken).AsTask();
    }

    public Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliveryItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public async Task<DeliveryDetailReadModel?> GetDetailAsync(
        Guid orderId,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _dbContext.DeliverySet
            .AsNoTracking()
            .Where(entity => entity.DeliveryId == deliveryId && entity.OrderId == orderId)
            .Select(entity => new DeliveryDetailReadModel
            {
                DeliveryId = entity.DeliveryId,
                OrderId = entity.OrderId,
                Status = entity.Status,
                CreatedBy = entity.CreatedBy,
                CompletedBy = entity.CompletedBy,
                Note = entity.Note,
                CreatedAt = entity.CreatedAt,
                CompletedAt = entity.CompletedAt,
                ItemCount = _dbContext.DeliveryItemSet.Count(item => item.DeliveryId == entity.DeliveryId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (delivery is null)
        {
            return null;
        }

        var items = await _dbContext.DeliveryItemSet
            .AsNoTracking()
            .Where(item => item.DeliveryId == deliveryId)
            .GroupJoin(
                _dbContext.OrderItemSet,
                deliveryItem => deliveryItem.OrderItemId,
                orderItem => orderItem.OrderItemId,
                (deliveryItem, orderItems) => new { deliveryItem, orderItems })
            .SelectMany(
                pair => pair.orderItems.DefaultIfEmpty(),
                (pair, orderItem) => new { pair.deliveryItem, orderItem })
            .GroupJoin(
                _dbContext.QuotationItemSet,
                pair => pair.orderItem != null ? pair.orderItem.QuotationItemId : null,
                quotationItem => quotationItem.QuotationItemId,
                (pair, quotationItems) => new { pair.deliveryItem, pair.orderItem, quotationItems })
            .SelectMany(
                pair => pair.quotationItems.DefaultIfEmpty(),
                (pair, quotationItem) => new DeliveryItemReadModel
                {
                    DeliveryItemId = pair.deliveryItem.DeliveryItemId,
                    DeliveryId = pair.deliveryItem.DeliveryId,
                    OrderItemId = pair.deliveryItem.OrderItemId,
                    Quantity = pair.deliveryItem.Quantity,
                    Note = pair.deliveryItem.Note,
                    ProductNameSnapshot = pair.orderItem != null ? pair.orderItem.ProductNameSnapshot : null,
                    ItemName = quotationItem != null
                        ? quotationItem.ItemName
                        : pair.orderItem != null ? pair.orderItem.ProductNameSnapshot : null
                })
            .OrderBy(item => item.DeliveryItemId)
            .ToListAsync(cancellationToken);

        return new DeliveryDetailReadModel
        {
            DeliveryId = delivery.DeliveryId,
            OrderId = delivery.OrderId,
            Status = delivery.Status,
            CreatedBy = delivery.CreatedBy,
            CompletedBy = delivery.CompletedBy,
            Note = delivery.Note,
            CreatedAt = delivery.CreatedAt,
            CompletedAt = delivery.CompletedAt,
            ItemCount = delivery.ItemCount,
            Items = items
        };
    }

    public async Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeliverySet
            .AsNoTracking()
            .Where(entity => entity.OrderId == orderId)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.DeliveryId)
            .Select(entity => new DeliveryListItemReadModel
            {
                DeliveryId = entity.DeliveryId,
                OrderId = entity.OrderId,
                Status = entity.Status,
                CreatedBy = entity.CreatedBy,
                CompletedBy = entity.CompletedBy,
                Note = entity.Note,
                CreatedAt = entity.CreatedAt,
                CompletedAt = entity.CompletedAt,
                ItemCount = _dbContext.DeliveryItemSet.Count(item => item.DeliveryId == entity.DeliveryId)
            })
            .ToListAsync(cancellationToken);
    }

    public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.FirstOrDefaultAsync(
            entity => entity.DeliveryId == deliveryId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeliveryItemSet
            .Where(item => item.DeliveryId == deliveryId)
            .OrderBy(item => item.DeliveryItemId)
            .ToListAsync(cancellationToken);
    }

    public void Update(Delivery delivery)
    {
        _dbContext.DeliverySet.Update(delivery);
    }
}
