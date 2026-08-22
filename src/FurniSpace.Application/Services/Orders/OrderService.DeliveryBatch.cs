using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.Orders;
using FurniSpace.Application.DTOs.Orders;
using static FurniSpace.Application.Constants.Orders.OrderServiceConstants;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using Mapster;
using System.Linq;

namespace FurniSpace.Application.Services.Orders;

public sealed partial class OrderService
{
    public async Task<ServiceResult<DeliveryDetailDto>> CreateDeliveryBatchAsync(
        Guid orderId,
        Guid currentUserId,
        CreateDeliveryBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<DeliveryDetailDto>.Unauthorized();
        }

        if (request.ProjectScheduleId == Guid.Empty)
        {
            return BadRequest<DeliveryDetailDto>(
                OrderErrorCodes.ProjectScheduleIdRequired,
                "Project schedule id is required for delivery batch creation.");
        }

        if (request.Items.Count == 0)
        {
            return BadRequest<DeliveryDetailDto>(
                OrderErrorCodes.DeliveryBatchEmpty,
                "At least one delivery item is required.");
        }

        var access = await ValidateDeliveryBatchStaffAccessAsync<DeliveryDetailDto>(
            orderId,
            currentUserId,
            cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var order = access.Order!;
        var project = access.Project!;
        var productionError = await ValidateProductionCompletedForDeliveryAsync<DeliveryDetailDto>(
            order.OrderId,
            cancellationToken);
        if (productionError is not null)
        {
            return productionError;
        }

        if (order.Status is not (OrderStatus.READY_FOR_DELIVERY or OrderStatus.DELIVERING))
        {
            return BadRequest<DeliveryDetailDto>(
                OrderErrorCodes.InvalidOrderStatus,
                "Order must be READY_FOR_DELIVERY or DELIVERING before a delivery batch can be created.");
        }

        var scheduleDetail = await _schedules.GetDetailAsync(request.ProjectScheduleId, cancellationToken);
        var scheduleError = await ValidateDeliveryBatchScheduleAsync<DeliveryDetailDto>(
            scheduleDetail,
            project.ProjectId,
            currentUserId,
            access.Role,
            cancellationToken);
        if (scheduleError is not null)
        {
            return scheduleError;
        }

        var duplicateItem = request.Items
            .GroupBy(item => item.OrderItemId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateItem is not null)
        {
            return BadRequest<DeliveryDetailDto>(
                OrderErrorCodes.DuplicateOrderItemInBatch,
                "Each order item can appear only once in a delivery batch.");
        }

        var orderItems = await _orders.GetItemsByOrderAsync(order.OrderId, cancellationToken);
        var orderItemsById = orderItems.ToDictionary(item => item.OrderItemId);
        var validationError = ValidateDeliveryBatchItems(request.Items, orderItemsById);
        if (validationError is not null)
        {
            return validationError;
        }

        var isFirstBatch = order.Status == OrderStatus.READY_FOR_DELIVERY;
        var now = DateTime.UtcNow;
        var deliveryId = Guid.NewGuid();
        var delivery = new Delivery
        {
            DeliveryId = deliveryId,
            OrderId = order.OrderId,
            ProjectScheduleId = request.ProjectScheduleId,
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedBy = currentUserId,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async transactionCancellationToken =>
            {
                if (isFirstBatch)
                {
                    order.Status = OrderStatus.DELIVERING;
                    project.Status = ProjectStatus.DELIVERING;
                    project.UpdatedAt = now;
                    _projects.Update(project);
                }

                await _deliveries.AddAsync(delivery, transactionCancellationToken);
                foreach (var item in request.Items)
                {
                    await _deliveries.AddItemAsync(
                        new DeliveryItem
                        {
                            DeliveryItemId = Guid.NewGuid(),
                            DeliveryId = deliveryId,
                            OrderItemId = item.OrderItemId,
                            Quantity = item.Quantity,
                            Note = item.Note
                        },
                        transactionCancellationToken);
                }

                order.UpdatedAt = now;
                _orders.Update(order);
                await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
            },
            cancellationToken);

        if (isFirstBatch)
        {
            await OrderNotificationSupport.TryDispatchUpdatedAsync(
                _notifications,
                _logger,
                order,
                project,
                cancellationToken);
            await OrderNotificationSupport.TryDispatchProjectStatusChangedAsync(
                _notifications,
                _logger,
                project,
                OrderNotificationSupport.BuildCustomerAndSalesReceivers(order, project),
                cancellationToken);
        }

        var detail = await _deliveries.GetDetailAsync(order.OrderId, deliveryId, cancellationToken);
        return ServiceResult<DeliveryDetailDto>.Created(
            detail!.Adapt<DeliveryDetailDto>(),
            "Delivery batch created successfully.");
    }

    public async Task<ServiceResult<DeliveryListResponseDto>> GetDeliveriesAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<DeliveryListResponseDto>.Unauthorized();
        }

        var accessError = await ValidateDeliveryBatchReadAccessAsync<DeliveryListResponseDto>(
            orderId,
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var items = await _deliveries.GetByOrderAsync(orderId, cancellationToken);
        return ServiceResult<DeliveryListResponseDto>.Success(
            new DeliveryListResponseDto
            {
                Items = items.Select(item => item.Adapt<DeliveryListItemDto>()).ToList()
            },
            "Delivery history retrieved successfully.");
    }

    public async Task<ServiceResult<DeliveryDetailDto>> GetDeliveryDetailAsync(
        Guid orderId,
        Guid deliveryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<DeliveryDetailDto>.Unauthorized();
        }

        var accessError = await ValidateDeliveryBatchReadAccessAsync<DeliveryDetailDto>(
            orderId,
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var detail = await _deliveries.GetDetailAsync(orderId, deliveryId, cancellationToken);
        return detail is null
            ? NotFound<DeliveryDetailDto>(OrderErrorCodes.DeliveryNotFound, "Delivery batch not found.")
            : ServiceResult<DeliveryDetailDto>.Success(
                detail.Adapt<DeliveryDetailDto>(),
                "Delivery batch detail retrieved successfully.");
    }

    public async Task<ServiceResult<DeliveryBatchCompletionDto>> CompleteDeliveryBatchAsync(
        Guid orderId,
        Guid deliveryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<DeliveryBatchCompletionDto>.Unauthorized();
        }

        var access = await ValidateDeliveryBatchStaffAccessAsync<DeliveryBatchCompletionDto>(
            orderId,
            currentUserId,
            cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var order = access.Order!;
        var project = access.Project!;
        var resolveResult = await ResolveCompleteDeliveryBatchContextAsync(
            order,
            deliveryId,
            cancellationToken);
        if (resolveResult.EarlyReturn is not null)
        {
            return resolveResult.EarlyReturn;
        }

        var delivery = resolveResult.Delivery!;
        var schedule = resolveResult.Schedule!;
        var deliveryItems = await _deliveries.GetItemsByDeliveryAsync(deliveryId, cancellationToken);
        var orderItemIds = deliveryItems.Select(item => item.OrderItemId).ToList();
        var now = DateTime.UtcNow;
        var updatedCount = 0;

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async transactionCancellationToken =>
            {
                var lockedItems = await _orders.GetItemsByIdsForUpdateAsync(orderItemIds, transactionCancellationToken);
                var lockedItemsById = lockedItems.ToDictionary(item => item.OrderItemId);

                foreach (var deliveryItem in deliveryItems)
                {
                    if (!lockedItemsById.TryGetValue(deliveryItem.OrderItemId, out var orderItem))
                    {
                        throw new InvalidOperationException(OrderErrorCodes.OrderItemNotFound);
                    }

                    ApplyDeliveryQuantity(orderItem, deliveryItem.Quantity, currentUserId, now);
                    _orders.UpdateItem(orderItem);
                    updatedCount++;
                }

                delivery.Status = DeliveryStatus.COMPLETED;
                delivery.CompletedBy = currentUserId;
                delivery.CompletedAt = now;
                delivery.UpdatedAt = now;

                schedule.Status = ProjectScheduleStatus.COMPLETED;
                schedule.CompletedAt = now;
                schedule.UpdatedAt = now;

                order.UpdatedAt = now;

                _deliveries.Update(delivery);
                _schedules.Update(schedule);
                _orders.Update(order);
                await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
            },
            cancellationToken);

        var remainingQuantity = await _orders.GetTotalRemainingDeliverableQuantityAsync(
            order.OrderId,
            cancellationToken);
        if (remainingQuantity == 0)
        {
            await CancelUnusedFutureDeliverySchedulesAsync(project.ProjectId, now, cancellationToken);
        }

        await OrderNotificationSupport.TryDispatchUpdatedAsync(
            _notifications,
            _logger,
            order,
            project,
            cancellationToken);

        return ServiceResult<DeliveryBatchCompletionDto>.Success(
            ToDeliveryBatchCompletionDto(delivery, updatedCount),
            "Delivery batch completed successfully.");
    }

    public async Task<ServiceResult<OrderDeliveryTrackingDto>> GetDeliveryTrackingAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDeliveryTrackingDto>.Unauthorized();
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderDeliveryTrackingDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var accessError = await ValidateDeliveryTrackingAccessAsync<OrderDeliveryTrackingDto>(
            order,
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var tracking = await _deliveries.GetTrackingByOrderAsync(orderId, order.ProjectId, cancellationToken);
        if (tracking is null)
        {
            return NotFound<OrderDeliveryTrackingDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        return ServiceResult<OrderDeliveryTrackingDto>.Success(
            tracking.Adapt<OrderDeliveryTrackingDto>(),
            "Delivery tracking retrieved successfully.");
    }

    private async Task<(Order? Order, Project? Project, string? Role, ServiceResult<T>? Error)> ValidateDeliveryBatchStaffAccessAsync<T>(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return (null, null, null, NotFound<T>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage));
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return (null, null, null, NotFound<T>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role == ApplicationRoles.Admin)
        {
            return (order, project, role, null);
        }

        if (role == ApplicationRoles.Production &&
            await _productionRequests.HasAssignedCompletedProductionForProjectAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken))
        {
            return (order, project, role, null);
        }

        return (null, null, null, ServiceResult<T>.Forbidden(ForbiddenMessage));
    }

    private sealed record CompleteDeliveryBatchContextResult(
        Delivery? Delivery,
        ProjectSchedule? Schedule,
        ServiceResult<DeliveryBatchCompletionDto>? EarlyReturn);

    private async Task<CompleteDeliveryBatchContextResult> ResolveCompleteDeliveryBatchContextAsync(
        Order order,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        var productionError = await ValidateProductionCompletedForDeliveryAsync<DeliveryBatchCompletionDto>(
            order.OrderId,
            cancellationToken);
        if (productionError is not null)
        {
            return new CompleteDeliveryBatchContextResult(null, null, productionError);
        }

        if (order.Status != OrderStatus.DELIVERING)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                BadRequest<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.OrderNotDelivering,
                    "Order must be DELIVERING before a delivery batch can be completed."));
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.OrderId != order.OrderId)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                NotFound<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.DeliveryNotFound,
                    "Delivery batch not found."));
        }

        if (delivery.Status == DeliveryStatus.COMPLETED)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                ServiceResult<DeliveryBatchCompletionDto>.Success(
                    ToDeliveryBatchCompletionDto(delivery, 0),
                    "Delivery batch already completed."));
        }

        if (delivery.Status != DeliveryStatus.IN_PROGRESS)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                BadRequest<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.DeliveryNotInProgress,
                    "Only in-progress delivery batches can be completed."));
        }

        if (!delivery.ProjectScheduleId.HasValue)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                BadRequest<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.DeliveryScheduleInvalid,
                    "Delivery batch must be linked to a delivery schedule."));
        }

        var schedule = await _schedules.GetByIdAsync(delivery.ProjectScheduleId.Value, cancellationToken);
        if (schedule is null ||
            schedule.ScheduleType != ProjectScheduleType.DELIVERY ||
            schedule.Status != ProjectScheduleStatus.CONFIRMED)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                BadRequest<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.DeliveryScheduleInvalid,
                    "Linked delivery schedule must exist and be confirmed."));
        }

        var deliveryItems = await _deliveries.GetItemsByDeliveryAsync(deliveryId, cancellationToken);
        if (deliveryItems.Count == 0)
        {
            return new CompleteDeliveryBatchContextResult(
                null,
                null,
                BadRequest<DeliveryBatchCompletionDto>(
                    OrderErrorCodes.DeliveryBatchEmpty,
                    "Delivery batch has no items to complete."));
        }

        return new CompleteDeliveryBatchContextResult(delivery, schedule, null);
    }

    private async Task<ServiceResult<T>?> ValidateDeliveryBatchScheduleAsync<T>(
        ProjectScheduleDetailReadModel? schedule,
        Guid projectId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken)
    {
        if (schedule is null || schedule.ProjectId != projectId)
        {
            return NotFound<T>(OrderErrorCodes.DeliveryScheduleInvalid, "Delivery schedule not found for this project.");
        }

        if (schedule.ScheduleType != ProjectScheduleType.DELIVERY)
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryScheduleInvalid,
                "Referenced schedule must be a delivery schedule.");
        }

        if (schedule.Status != ProjectScheduleStatus.CONFIRMED)
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryScheduleNotConfirmed,
                "Delivery schedule must be confirmed before batch creation.");
        }

        if (DateTime.UtcNow < schedule.ScheduledStart.Subtract(OrderDeliveryConstants.ScheduleStartTolerance))
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryScheduleNotStarted,
                "Delivery batch cannot start before the scheduled start time.");
        }

        if (await _deliveries.ExistsByProjectScheduleIdAsync(schedule.ScheduleId, cancellationToken))
        {
            return ServiceResult<T>.Failure(
                Error.Conflict(
                    OrderErrorCodes.DeliveryScheduleAlreadyUsed,
                    "This delivery schedule is already linked to a delivery batch."));
        }

        if (role != ApplicationRoles.Admin &&
            schedule.AssignedStaffId != currentUserId)
        {
            return ServiceResult<T>.Forbidden(ForbiddenMessage);
        }

        return null;
    }

    private async Task<ServiceResult<T>?> ValidateDeliveryBatchReadAccessAsync<T>(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<T>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        return await ValidateDeliveryTrackingAccessAsync<T>(order, currentUserId, cancellationToken);
    }

    private async Task<ServiceResult<T>?> ValidateDeliveryTrackingAccessAsync<T>(
        Order order,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await _projects.GetDetailAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<T>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role == ApplicationRoles.Admin)
        {
            return null;
        }

        if (role == ApplicationRoles.Customer && project.CustomerId == currentUserId)
        {
            return null;
        }

        if (role == ApplicationRoles.Sales && project.AssignedSalesId == currentUserId)
        {
            return null;
        }

        if (role == ApplicationRoles.Production &&
            await _productionRequests.HasViewableAssignedRequestAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken))
        {
            return null;
        }

        if (OrderAccessEvaluator.CanViewOrder(
                role,
                project.CustomerId,
                project.AssignedSalesId,
                project.AssignedDesignerId,
                currentUserId,
                order.Status))
        {
            return null;
        }

        return ServiceResult<T>.Forbidden(ForbiddenMessage);
    }

    private async Task CancelUnusedFutureDeliverySchedulesAsync(
        Guid projectId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var schedules = await _schedules.GetUnusedFutureDeliverySchedulesAsync(projectId, cancellationToken);
        if (schedules.Count == 0)
        {
            return;
        }

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async transactionCancellationToken =>
            {
                foreach (var schedule in schedules)
                {
                    schedule.Status = ProjectScheduleStatus.CANCELLED;
                    schedule.CancelledAt = now;
                    schedule.UpdatedAt = now;
                    schedule.InternalNote = OrderDeliveryConstants.AllItemsAlreadyDeliveredCancellationNote;
                    _schedules.Update(schedule);
                }

                await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
            },
            cancellationToken);
    }

    private static ServiceResult<DeliveryDetailDto>? ValidateDeliveryBatchItems(
        IReadOnlyList<CreateDeliveryBatchItemRequestDto> items,
        Dictionary<Guid, OrderItem> orderItemsById)
    {
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest<DeliveryDetailDto>(
                    OrderErrorCodes.InvalidDeliveryQuantity,
                    "Delivery quantity must be greater than zero.");
            }

            if (!orderItemsById.TryGetValue(item.OrderItemId, out var orderItem))
            {
                return NotFound<DeliveryDetailDto>(
                    OrderErrorCodes.OrderItemNotFound,
                    "One or more order items were not found for this order.");
            }

            if (!IsBatchEligibleOrderItem(orderItem))
            {
                return BadRequest<DeliveryDetailDto>(
                    OrderErrorCodes.OrderItemNotDeliverable,
                    "One or more order items are not eligible for delivery.");
            }

            var remainingQuantity = GetRemainingDeliverableQuantity(orderItem);
            if (item.Quantity > remainingQuantity)
            {
                return ServiceResult<DeliveryDetailDto>.Failure(
                    Error.Conflict(
                        OrderErrorCodes.InvalidDeliveryQuantity,
                        "Delivery quantity exceeds remaining deliverable quantity."));
            }
        }

        return null;
    }

    private static void ApplyDeliveryQuantity(
        OrderItem orderItem,
        int increment,
        Guid currentUserId,
        DateTime now)
    {
        var quantity = orderItem.Quantity ?? 0;
        var previousDeliveredQuantity = orderItem.DeliveredQuantity;
        var newDeliveredQuantity = previousDeliveredQuantity + increment;
        if (newDeliveredQuantity < 0 || newDeliveredQuantity > quantity)
        {
            throw new InvalidOperationException(OrderErrorCodes.InvalidDeliveryQuantity);
        }

        orderItem.DeliveredQuantity = newDeliveredQuantity;
        if (previousDeliveredQuantity < quantity && newDeliveredQuantity >= quantity)
        {
            orderItem.DeliveredAt = now;
            orderItem.DeliveredBy = currentUserId;
        }

        orderItem.Status = newDeliveredQuantity < quantity
            ? OrderItemStatus.PARTIALLY_DELIVERED
            : OrderItemStatus.READY;
    }

    private static int GetRemainingDeliverableQuantity(OrderItem item)
    {
        var quantity = item.Quantity ?? 0;
        return Math.Max(0, quantity - item.DeliveredQuantity);
    }

    private static bool IsBatchEligibleOrderItem(OrderItem item)
    {
        return IsProductLineItem(item) &&
            item.Status is OrderItemStatus.READY or OrderItemStatus.PARTIALLY_DELIVERED &&
            GetRemainingDeliverableQuantity(item) > 0;
    }

    private static DeliveryBatchCompletionDto ToDeliveryBatchCompletionDto(
        Delivery delivery,
        int updatedItemCount)
    {
        return new DeliveryBatchCompletionDto
        {
            DeliveryId = delivery.DeliveryId,
            OrderId = delivery.OrderId,
            Status = delivery.Status,
            UpdatedItemCount = updatedItemCount,
            CompletedAt = delivery.CompletedAt
        };
    }
}
