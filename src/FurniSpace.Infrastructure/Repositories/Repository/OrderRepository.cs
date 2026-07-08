using FurniSpace.Domain.Entities;
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

    public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
    {
        return DbContext.OrderItemSet.AddAsync(item, cancellationToken).AsTask();
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
