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

    public Task<OrderAdjustment?> GetAdjustmentByIdAsync(
        Guid orderAdjustmentId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderAdjustmentSet.FirstOrDefaultAsync(
            adjustment => adjustment.OrderAdjustmentId == orderAdjustmentId,
            cancellationToken);
    }

    public Task<OrderAdjustmentItem?> GetAdjustmentItemByIdAsync(
        Guid orderAdjustmentItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderAdjustmentItemSet.FirstOrDefaultAsync(
            item => item.OrderAdjustmentItemId == orderAdjustmentItemId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OrderAdjustmentItem>> GetAdjustmentItemsAsync(
        Guid orderAdjustmentId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.OrderAdjustmentItemSet
            .Where(item => item.OrderAdjustmentId == orderAdjustmentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderAdjustment>> GetAdjustmentsByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.OrderAdjustmentSet
            .Where(adjustment => adjustment.OrderId == orderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderAdjustmentItem>> GetAdjustmentItemsByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from item in DbContext.OrderAdjustmentItemSet
                join adjustment in DbContext.OrderAdjustmentSet
                    on item.OrderAdjustmentId equals adjustment.OrderAdjustmentId
                where adjustment.OrderId == orderId
                select item)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasCancelledProductionItemAsync(
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionItemSet.AnyAsync(
            item => item.OrderItemId == orderItemId && item.Status == ProductionItemStatus.CANCELLED,
            cancellationToken);
    }

    public Task AddAdjustmentAsync(
        OrderAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderAdjustmentSet.AddAsync(adjustment, cancellationToken).AsTask();
    }

    public Task AddAdjustmentItemAsync(
        OrderAdjustmentItem item,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderAdjustmentItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public void UpdateAdjustment(OrderAdjustment adjustment)
    {
        DbContext.OrderAdjustmentSet.Update(adjustment);
    }

    public void UpdateAdjustmentItem(OrderAdjustmentItem item)
    {
        DbContext.OrderAdjustmentItemSet.Update(item);
    }

    public void UpdateItem(OrderItem item)
    {
        DbContext.OrderItemSet.Update(item);
    }

    public void RemoveAdjustmentItem(OrderAdjustmentItem item)
    {
        DbContext.OrderAdjustmentItemSet.Remove(item);
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
                    ItemType = quotationItem != null ? quotationItem.ItemType : null,
                    ProductNameSnapshot = pair.orderItem.ProductNameSnapshot,
                    ItemName = quotationItem != null ? quotationItem.ItemName : pair.orderItem.ProductNameSnapshot,
                    Quantity = pair.orderItem.Quantity,
                    Status = pair.orderItem.Status,
                    DeliveredQuantity = pair.orderItem.DeliveredQuantity,
                    CustomerConfirmedAt = pair.orderItem.CustomerConfirmedAt,
                    UnitPrice = pair.orderItem.UnitPrice,
                    CustomizationAdditionalCost = pair.orderItem.CustomizationFee,
                    DiscountAmount = pair.orderItem.DiscountAmount,
                    SubtotalAmount = pair.orderItem.SubtotalAmount,
                    IsCustomized = quotationItem != null ? quotationItem.IsCustomized : null
                })
            .OrderBy(item => item.OrderItemId)
            .ToListAsync(cancellationToken);
    }
}
