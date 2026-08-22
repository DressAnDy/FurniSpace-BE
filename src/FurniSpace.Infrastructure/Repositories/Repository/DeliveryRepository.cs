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
    private const string AllItemsAlreadyDeliveredNote = "ALL_ITEMS_ALREADY_DELIVERED";

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

    public Task<DeliveryDetailReadModel?> GetDetailAsync(
        Guid orderId,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return BuildDetailQuery(orderId, deliveryId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet
            .AsNoTracking()
            .Where(entity => entity.OrderId == orderId)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.DeliveryId)
            .Select(entity => new DeliveryListItemReadModel
            {
                DeliveryId = entity.DeliveryId,
                OrderId = entity.OrderId,
                ProjectScheduleId = entity.ProjectScheduleId,
                Schedule = entity.ProjectScheduleId == null
                    ? null
                    : _dbContext.ProjectScheduleSet
                        .Where(schedule => schedule.ScheduleId == entity.ProjectScheduleId)
                        .Select(schedule => new DeliveryScheduleSummaryReadModel
                        {
                            ProjectScheduleId = schedule.ScheduleId,
                            ScheduledStart = schedule.ScheduledStart,
                            ScheduledEnd = schedule.ScheduledEnd,
                            CompletedAt = schedule.CompletedAt,
                            Status = schedule.Status,
                            AssignedStaffId = schedule.AssignedStaffId
                        })
                        .FirstOrDefault(),
                Status = entity.Status,
                CreatedBy = entity.CreatedBy,
                CompletedBy = entity.CompletedBy,
                Note = entity.Note,
                CreatedAt = entity.CreatedAt,
                CompletedAt = entity.CompletedAt,
                ItemCount = _dbContext.DeliveryItemSet.Count(item => item.DeliveryId == entity.DeliveryId)
            })
            .ToListAsync(cancellationToken)
            .ContinueWith(task => (IReadOnlyList<DeliveryListItemReadModel>)task.Result, cancellationToken);
    }

    public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.FirstOrDefaultAsync(
            entity => entity.DeliveryId == deliveryId,
            cancellationToken);
    }

    public Task<Delivery?> GetByProjectScheduleIdAsync(
        Guid projectScheduleId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.FirstOrDefaultAsync(
            entity => entity.ProjectScheduleId == projectScheduleId,
            cancellationToken);
    }

    public Task<bool> ExistsByProjectScheduleIdAsync(
        Guid projectScheduleId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.AnyAsync(
            entity => entity.ProjectScheduleId == projectScheduleId,
            cancellationToken);
    }

    public Task<bool> HasInProgressDeliveryAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliverySet.AnyAsync(
            entity => entity.OrderId == orderId && entity.Status == DeliveryStatus.IN_PROGRESS,
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

    public async Task<OrderDeliveryTrackingReadModel?> GetTrackingByOrderAsync(
        Guid orderId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.OrderSet
            .AsNoTracking()
            .Where(entity => entity.OrderId == orderId && entity.ProjectId == projectId)
            .Select(entity => new { entity.OrderId, entity.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var orderItems = await _dbContext.OrderItemSet
            .AsNoTracking()
            .Where(item =>
                item.OrderId == orderId &&
                item.ProductVersionId.HasValue &&
                (item.Quantity ?? 0) > 0 &&
                item.Status != OrderItemStatus.UNAVAILABLE &&
                item.Status != OrderItemStatus.CANCELLED)
            .GroupJoin(
                _dbContext.QuotationItemSet,
                orderItem => orderItem.QuotationItemId,
                quotationItem => quotationItem.QuotationItemId,
                (orderItem, quotationItems) => new { orderItem, quotationItems })
            .SelectMany(
                pair => pair.quotationItems.DefaultIfEmpty(),
                (pair, quotationItem) => new OrderDeliveryTrackingItemReadModel
                {
                    OrderItemId = pair.orderItem.OrderItemId,
                    ProductName = quotationItem != null
                        ? quotationItem.ItemName
                        : pair.orderItem.ProductNameSnapshot,
                    OrderedQuantity = pair.orderItem.Quantity ?? 0,
                    DeliveredQuantity = pair.orderItem.DeliveredQuantity,
                    RemainingQuantity = Math.Max(0, (pair.orderItem.Quantity ?? 0) - pair.orderItem.DeliveredQuantity),
                    Status = pair.orderItem.Status
                })
            .ToListAsync(cancellationToken);

        var totalOrdered = orderItems.Sum(item => item.OrderedQuantity);
        var totalDelivered = orderItems.Sum(item => item.DeliveredQuantity);
        var remaining = Math.Max(0, totalOrdered - totalDelivered);

        var deliverySchedules = await _dbContext.ProjectScheduleSet
            .AsNoTracking()
            .Where(schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY)
            .OrderBy(schedule => schedule.ScheduledStart)
            .ThenBy(schedule => schedule.ScheduleId)
            .Select(schedule => new
            {
                schedule.ScheduleId,
                schedule.ScheduledStart,
                schedule.ScheduledEnd,
                schedule.Status,
                schedule.CompletedAt,
                schedule.InternalNote,
                Delivery = _dbContext.DeliverySet
                    .Where(delivery => delivery.ProjectScheduleId == schedule.ScheduleId)
                    .Select(delivery => new
                    {
                        delivery.DeliveryId,
                        delivery.Status,
                        delivery.CompletedAt
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var completedDeliveryCount = deliverySchedules.Count(entry =>
            entry.Delivery?.Status == DeliveryStatus.COMPLETED);

        var upcomingSchedules = deliverySchedules
            .Where(entry =>
                entry.Status is ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED)
            .ToList();

        var timeline = new List<OrderDeliveryTrackingTimelineEntryReadModel>();
        foreach (var entry in deliverySchedules)
        {
            IReadOnlyList<OrderDeliveryTrackingTimelineItemReadModel> timelineItems = [];
            if (entry.Delivery is not null)
            {
                timelineItems = await _dbContext.DeliveryItemSet
                    .AsNoTracking()
                    .Where(item => item.DeliveryId == entry.Delivery.DeliveryId)
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
                        (pair, quotationItem) => new OrderDeliveryTrackingTimelineItemReadModel
                        {
                            OrderItemId = pair.deliveryItem.OrderItemId,
                            ProductName = quotationItem != null
                                ? quotationItem.ItemName
                                : pair.orderItem != null ? pair.orderItem.ProductNameSnapshot : null,
                            DeliveredQuantity = pair.deliveryItem.Quantity
                        })
                    .ToListAsync(cancellationToken);
            }

            timeline.Add(new OrderDeliveryTrackingTimelineEntryReadModel
            {
                ProjectScheduleId = entry.ScheduleId,
                DeliveryId = entry.Delivery?.DeliveryId,
                ScheduledStart = entry.ScheduledStart,
                ScheduledEnd = entry.ScheduledEnd,
                ScheduleStatus = entry.Status,
                DeliveryStatus = entry.Delivery?.Status,
                CompletedAt = entry.Delivery?.CompletedAt ?? entry.CompletedAt,
                CancelReason = entry.Status == ProjectScheduleStatus.CANCELLED &&
                    string.Equals(entry.InternalNote, AllItemsAlreadyDeliveredNote, StringComparison.Ordinal)
                    ? AllItemsAlreadyDeliveredNote
                    : null,
                Items = timelineItems
            });
        }

        return new OrderDeliveryTrackingReadModel
        {
            OrderId = order.OrderId,
            OrderStatus = order.Status,
            TotalOrderedQuantity = totalOrdered,
            TotalDeliveredQuantity = totalDelivered,
            RemainingQuantity = remaining,
            DeliveryProgressPercent = totalOrdered > 0
                ? (int)Math.Round(totalDelivered * 100m / totalOrdered, MidpointRounding.AwayFromZero)
                : 0,
            CompletedDeliveryCount = completedDeliveryCount,
            UpcomingDeliveryCount = upcomingSchedules.Count,
            NextDeliveryAt = upcomingSchedules.FirstOrDefault()?.ScheduledStart,
            Items = orderItems,
            Timeline = timeline
        };
    }

    public async Task<ProjectDeliverySummaryReadModel?> GetProjectDeliverySummaryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.ProjectSet
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Select(entity => new { entity.ProjectId, entity.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null ||
            project.Status is not (ProjectStatus.READY_FOR_DELIVERY or ProjectStatus.DELIVERING or ProjectStatus.DELIVERED))
        {
            return null;
        }

        var orderIds = await _dbContext.OrderSet
            .AsNoTracking()
            .Where(order => order.ProjectId == projectId)
            .Select(order => order.OrderId)
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
        {
            return null;
        }

        var orderItems = await _dbContext.OrderItemSet
            .AsNoTracking()
            .Where(item =>
                orderIds.Contains(item.OrderId) &&
                item.ProductVersionId.HasValue &&
                (item.Quantity ?? 0) > 0 &&
                item.Status != OrderItemStatus.UNAVAILABLE &&
                item.Status != OrderItemStatus.CANCELLED)
            .Select(item => new { item.Quantity, item.DeliveredQuantity })
            .ToListAsync(cancellationToken);

        var totalQuantity = orderItems.Sum(item => item.Quantity ?? 0);
        var deliveredQuantity = orderItems.Sum(item => item.DeliveredQuantity);
        var remainingQuantity = Math.Max(0, totalQuantity - deliveredQuantity);

        var nextDeliveryAt = await _dbContext.ProjectScheduleSet
            .AsNoTracking()
            .Where(schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
                 schedule.Status == ProjectScheduleStatus.CONFIRMED))
            .OrderBy(schedule => schedule.ScheduledStart)
            .Select(schedule => (DateTime?)schedule.ScheduledStart)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProjectDeliverySummaryReadModel
        {
            Status = project.Status,
            TotalQuantity = totalQuantity,
            DeliveredQuantity = deliveredQuantity,
            RemainingQuantity = remainingQuantity,
            DeliveryProgressPercent = totalQuantity > 0
                ? (int)Math.Round(deliveredQuantity * 100m / totalQuantity, MidpointRounding.AwayFromZero)
                : 0,
            NextDeliveryAt = nextDeliveryAt
        };
    }

    public void Update(Delivery delivery)
    {
        _dbContext.DeliverySet.Update(delivery);
    }

    private IQueryable<DeliveryDetailReadModel> BuildDetailQuery(Guid orderId, Guid deliveryId)
    {
        return _dbContext.DeliverySet
            .AsNoTracking()
            .Where(entity => entity.DeliveryId == deliveryId && entity.OrderId == orderId)
            .Select(entity => new DeliveryDetailReadModel
            {
                DeliveryId = entity.DeliveryId,
                OrderId = entity.OrderId,
                ProjectScheduleId = entity.ProjectScheduleId,
                Schedule = entity.ProjectScheduleId == null
                    ? null
                    : _dbContext.ProjectScheduleSet
                        .Where(schedule => schedule.ScheduleId == entity.ProjectScheduleId)
                        .Select(schedule => new DeliveryScheduleSummaryReadModel
                        {
                            ProjectScheduleId = schedule.ScheduleId,
                            ScheduledStart = schedule.ScheduledStart,
                            ScheduledEnd = schedule.ScheduledEnd,
                            CompletedAt = schedule.CompletedAt,
                            Status = schedule.Status,
                            AssignedStaffId = schedule.AssignedStaffId
                        })
                        .FirstOrDefault(),
                Status = entity.Status,
                CreatedBy = entity.CreatedBy,
                CompletedBy = entity.CompletedBy,
                Note = entity.Note,
                CreatedAt = entity.CreatedAt,
                CompletedAt = entity.CompletedAt,
                ItemCount = _dbContext.DeliveryItemSet.Count(item => item.DeliveryId == entity.DeliveryId),
                Items = _dbContext.DeliveryItemSet
                    .Where(item => item.DeliveryId == entity.DeliveryId)
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
                    .ToList()
            });
    }
}
