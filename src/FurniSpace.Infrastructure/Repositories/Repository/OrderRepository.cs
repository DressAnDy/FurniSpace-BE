using System.Diagnostics.CodeAnalysis;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await BuildListQuery()
            .Where(order => order.ProjectId == projectId)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderDetailReadModel?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await DbContext.OrderSet
            .Where(item => item.OrderId == orderId)
            .Join(
                DbContext.ProjectSet,
                order => order.ProjectId,
                project => project.ProjectId,
                (order, project) => new OrderDetailReadModel
                {
                    OrderId = order.OrderId,
                    ProjectId = order.ProjectId,
                    ProposalId = order.ProposalId,
                    QuotationId = order.QuotationId,
                    OrderCode = order.OrderCode,
                    CustomerId = order.CustomerId,
                    SalesId = order.SalesId,
                    VatRate = order.VatRate,
                    VatAmount = order.VatAmount,
                    OriginalTotalAmount = order.OriginalTotalAmount,
                    ItemAdjustmentAmount = order.ItemAdjustmentAmount,
                    AdditionalDiscountAmount = order.AdditionalDiscountAmount,
                    FinalTotalAmount = order.FinalTotalAmount,
                    DepositAmount = order.DepositAmount,
                    PaidAmount = order.PaidAmount,
                    RemainingAmount = order.RemainingAmount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        order.Items = await GetItemsAsync(orderId, cancellationToken);
        return order;
    }

    public new Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return DbContext.OrderSet.FirstOrDefaultAsync(order => order.OrderId == orderId, cancellationToken);
    }

    public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        return DbContext.OrderSet.AnyAsync(order => order.QuotationId == quotationId, cancellationToken);
    }

    public Task<bool> HasProjectOrderInStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<OrderStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderSet.AnyAsync(
            order =>
                order.ProjectId == projectId &&
                order.Status.HasValue &&
                statuses.Contains(order.Status.Value),
            cancellationToken);
    }

    public Task<Order?> GetLatestByProjectInStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<OrderStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderSet
            .Where(order =>
                order.ProjectId == projectId &&
                order.Status.HasValue &&
                statuses.Contains(order.Status.Value))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
    {
        return DbContext.OrderItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public Task<OrderItem?> GetItemByIdAsync(
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderItemSet.FirstOrDefaultAsync(
            item => item.OrderItemId == orderItemId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OrderItem>> GetItemsByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.OrderItemSet
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);
    }

    public void UpdateItem(OrderItem item)
    {
        DbContext.OrderItemSet.Update(item);
    }

    public async Task<OrderItem?> TryIncrementDeliveredQuantityAsync(
        Guid orderItemId,
        int increment,
        string? deliveryNote,
        Guid deliveredBy,
        DateTime deliveredAt,
        CancellationToken cancellationToken = default)
    {
        if (!DbContext.Database.IsRelational())
        {
            return await TryIncrementDeliveredQuantityInMemoryAsync(
                orderItemId,
                increment,
                deliveryNote,
                deliveredBy,
                deliveredAt,
                cancellationToken);
        }

        return await TryIncrementDeliveredQuantityRelationalAsync(
            orderItemId,
            increment,
            deliveryNote,
            deliveredBy,
            deliveredAt,
            cancellationToken);
    }

    [ExcludeFromCodeCoverage(Justification = "Provider-specific atomic SQL update is covered by API integration tests.")]
    private async Task<OrderItem?> TryIncrementDeliveredQuantityRelationalAsync(
        Guid orderItemId,
        int increment,
        string? deliveryNote,
        Guid deliveredBy,
        DateTime deliveredAt,
        CancellationToken cancellationToken)
    {
        var updated = await DbContext.OrderItemSet
            .Where(item =>
                item.OrderItemId == orderItemId &&
                item.Status == OrderItemStatus.READY &&
                (item.Quantity ?? 0) > 0 &&
                (item.DeliveredQuantity ?? 0) + increment <= (item.Quantity ?? 0))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveredQuantity, item => (item.DeliveredQuantity ?? 0) + increment)
                .SetProperty(item => item.DeliveryNote, deliveryNote)
                .SetProperty(item => item.LastDeliveredAt, deliveredAt)
                .SetProperty(item => item.LastDeliveredBy, deliveredBy),
                cancellationToken);

        return updated == 0
            ? null
            : await GetItemByIdAsync(orderItemId, cancellationToken);
    }

    private async Task<OrderItem?> TryIncrementDeliveredQuantityInMemoryAsync(
        Guid orderItemId,
        int increment,
        string? deliveryNote,
        Guid deliveredBy,
        DateTime deliveredAt,
        CancellationToken cancellationToken)
    {
        var item = await GetItemByIdAsync(orderItemId, cancellationToken);
        if (item is null || item.Status != OrderItemStatus.READY)
        {
            return null;
        }

        var quantity = item.Quantity ?? 0;
        var deliveredQuantity = item.DeliveredQuantity ?? 0;
        if (quantity <= 0 || deliveredQuantity + increment > quantity)
        {
            return null;
        }

        item.DeliveredQuantity = deliveredQuantity + increment;
        item.DeliveryNote = deliveryNote;
        item.LastDeliveredAt = deliveredAt;
        item.LastDeliveredBy = deliveredBy;
        UpdateItem(item);
        return item;
    }

    public new void Update(Order order)
    {
        DbContext.OrderSet.Update(order);
    }

    private IQueryable<OrderListItemReadModel> BuildListQuery()
    {
        return DbContext.OrderSet
            .Join(
                DbContext.ProjectSet,
                order => order.ProjectId,
                project => project.ProjectId,
                (order, project) => new OrderListItemReadModel
                {
                    OrderId = order.OrderId,
                    ProjectId = order.ProjectId,
                    QuotationId = order.QuotationId,
                    OrderCode = order.OrderCode,
                    OriginalTotalAmount = order.OriginalTotalAmount,
                    DepositAmount = order.DepositAmount,
                    PaidAmount = order.PaidAmount,
                    RemainingAmount = order.RemainingAmount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    CustomerId = order.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                });
    }

    private async Task<IReadOnlyList<OrderItemDetailReadModel>> GetItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await DbContext.OrderItemSet
            .Where(item => item.OrderId == orderId)
            .GroupJoin(
                DbContext.QuotationItemSet,
                orderItem => orderItem.QuotationItemId,
                quotationItem => quotationItem.QuotationItemId,
                (orderItem, quotationItems) => new { orderItem, quotationItems })
            .SelectMany(
                pair => pair.quotationItems.DefaultIfEmpty(),
                (pair, quotationItem) => new OrderItemDetailReadModel
                {
                    OrderItemId = pair.orderItem.OrderItemId,
                    ProductNameSnapshot = pair.orderItem.ProductNameSnapshot,
                    ItemName = quotationItem != null ? quotationItem.ItemName : pair.orderItem.ProductNameSnapshot,
                    Quantity = pair.orderItem.Quantity,
                    Status = pair.orderItem.Status,
                    DeliveredQuantity = pair.orderItem.DeliveredQuantity,
                    CustomerConfirmedAt = pair.orderItem.CustomerConfirmedAt,
                    UnitPrice = pair.orderItem.UnitPrice,
                    DiscountAmount = pair.orderItem.DiscountAmount,
                    SubtotalAmount = pair.orderItem.SubtotalAmount,
                    IsCustomized = quotationItem != null ? quotationItem.IsCustomized : null
                })
            .OrderBy(item => item.OrderItemId)
            .ToListAsync(cancellationToken);
    }
}
