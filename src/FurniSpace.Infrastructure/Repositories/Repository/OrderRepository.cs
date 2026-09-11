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

    public async Task<IReadOnlyList<CustomerMyOrderListItemReadModel>> GetByCustomerPagedAsync(
        Guid customerId,
        CustomerMyOrdersQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildCustomerMyOrdersQuery(customerId, query)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByCustomerAsync(
        Guid customerId,
        CustomerMyOrdersQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildCustomerMyOrdersQuery(customerId, query).CountAsync(cancellationToken);
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
                    TotalAmount = order.FinalTotalAmount,
                    DepositAmount = order.DepositAmount,
                    PaidAmount = order.PaidAmount,
                    RemainingAmount = order.RemainingAmount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    CustomerConfirmedDeliveryAt = order.CustomerConfirmedDeliveryAt,
                    DeliveryAddress = order.DeliveryAddress,
                    ReceiverName = order.ReceiverName,
                    ReceiverPhone = order.ReceiverPhone,
                    DeliveryNote = order.DeliveryNote,
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

    public async Task<bool> HasCompletedDeliveryFlowAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var orders = await DbContext.OrderSet
            .AsNoTracking()
            .Where(order => order.ProjectId == projectId)
            .Select(order => new { order.OrderId, order.Status, order.CustomerConfirmedDeliveryAt })
            .ToListAsync(cancellationToken);

        if (orders.Any(order =>
                order.Status is OrderStatus.DELIVERED or OrderStatus.FINAL_PAYMENT_PENDING or OrderStatus.COMPLETED ||
                order.CustomerConfirmedDeliveryAt.HasValue))
        {
            return true;
        }

        var orderIds = orders.Select(order => order.OrderId).ToList();
        if (orderIds.Count == 0)
        {
            return false;
        }

        return await DbContext.OrderItemSet.AnyAsync(
            item =>
                orderIds.Contains(item.OrderId) &&
                item.ProductVersionId.HasValue &&
                (item.Quantity ?? 0) > 0 &&
                item.Status == OrderItemStatus.DELIVERED,
            cancellationToken);
    }

    public async Task<bool> AllDeliverableItemsReadyAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetItemsByOrderAsync(orderId, cancellationToken);
        var deliverableItems = items.Where(IsDeliverableItem).ToList();
        return deliverableItems.Count > 0 &&
            deliverableItems.All(item =>
                item.Status == OrderItemStatus.READY &&
                item.DeliveredQuantity == 0);
    }

    public async Task<bool> AllDeliverableItemsDeliveredAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetItemsByOrderAsync(orderId, cancellationToken);
        var deliverableItems = items.Where(IsDeliverableItem).ToList();
        return deliverableItems.Count > 0 &&
            deliverableItems.All(IsFullyDelivered);
    }

    public async Task<bool> AllDeliverableItemsPhysicallyDeliveredAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetItemsByOrderAsync(orderId, cancellationToken);
        var deliverableItems = items.Where(IsDeliverableItem).ToList();
        return deliverableItems.Count > 0 &&
            deliverableItems.All(item => item.Status == OrderItemStatus.PHYSICALLY_DELIVERED);
    }

    public async Task<int> GetTotalRemainingDeliverableQuantityAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetItemsByOrderAsync(orderId, cancellationToken);
        return items
            .Where(IsDeliverableItem)
            .Sum(item => Math.Max(0, (item.Quantity ?? 0) - item.DeliveredQuantity));
    }

    public async Task<IReadOnlyList<OrderItem>> GetItemsByIdsForUpdateAsync(
        IReadOnlyCollection<Guid> orderItemIds,
        CancellationToken cancellationToken = default)
    {
        if (orderItemIds.Count == 0)
        {
            return [];
        }

        return await DbContext.OrderItemSet
            .FromSqlInterpolated(
                $"SELECT * FROM order_items WHERE order_item_id = ANY({orderItemIds.ToArray()}) FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    private static bool IsFullyDelivered(OrderItem item)
    {
        if (item.Status is OrderItemStatus.DELIVERED or OrderItemStatus.PHYSICALLY_DELIVERED)
        {
            return true;
        }

        var quantity = item.Quantity ?? 0;
        return quantity > 0 && item.DeliveredQuantity >= quantity;
    }

    private static bool IsDeliverableItem(OrderItem item)
    {
        return item.ProductVersionId.HasValue &&
            (item.Quantity ?? 0) > 0 &&
            item.Status is not (OrderItemStatus.UNAVAILABLE or OrderItemStatus.CANCELLED);
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
                    TotalAmount = order.FinalTotalAmount,
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

    private IQueryable<CustomerMyOrderListItemReadModel> BuildCustomerMyOrdersQuery(
        Guid customerId,
        CustomerMyOrdersQueryReadModel query)
    {
        var orders = DbContext.OrderSet
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId);

        if (query.Status.HasValue)
        {
            orders = orders.Where(order => order.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(order => order.OrderCode.Contains(search));
        }

        return orders.Select(order => new CustomerMyOrderListItemReadModel
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            ProjectId = order.ProjectId,
            Status = order.Status,
            TotalAmount = order.FinalTotalAmount,
            DepositAmount = order.DepositAmount,
            PaidAmount = order.PaidAmount,
            RemainingAmount = order.RemainingAmount,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
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
                    DeliveredQuantity = pair.orderItem.DeliveredQuantity,
                    Status = pair.orderItem.Status,
                    DeliveredAt = pair.orderItem.DeliveredAt,
                    DeliveredBy = pair.orderItem.DeliveredBy,
                    UnitPrice = pair.orderItem.UnitPrice,
                    DiscountAmount = pair.orderItem.DiscountAmount,
                    SubtotalAmount = pair.orderItem.SubtotalAmount,
                    IsCustomized = quotationItem != null ? quotationItem.IsCustomized : null
                })
            .OrderBy(item => item.OrderItemId)
            .ToListAsync(cancellationToken);
    }
}
