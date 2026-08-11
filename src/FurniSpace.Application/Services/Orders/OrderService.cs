using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Projects;
using static FurniSpace.Application.Constants.Orders.OrderServiceConstants;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Orders;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IProjectRepository _projects;
    private readonly IPaymentRepository _payments;
    private readonly IProjectScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orders,
        IProjectRepository projects,
        IPaymentRepository payments,
        IProjectScheduleRepository schedules,
        IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _projects = projects;
        _payments = payments;
        _schedules = schedules;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<OrderListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderListResponseDto>.Unauthorized();
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFoundList(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var items = await _orders.GetByProjectAsync(projectId, cancellationToken);
        var filtered = FilterByAccess(items, role, currentUserId);

        return ServiceResult<OrderListResponseDto>.Success(
            new OrderListResponseDto
            {
                Items = filtered.ConvertAll(item => item.Adapt<OrderListItemDto>())
            },
            "Orders retrieved successfully.");
    }

    public async Task<ServiceResult<OrderDetailDto>> GetDetailAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDetailDto>.Unauthorized();
        }

        var order = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFoundDetail(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!OrderAccessEvaluator.CanViewOrder(
                role,
                order.CustomerId,
                order.AssignedSalesId,
                order.AssignedDesignerId,
                currentUserId,
                order.Status))
        {
            return ServiceResult<OrderDetailDto>.Forbidden("You do not have access to this order.");
        }

        return ServiceResult<OrderDetailDto>.Success(
            order.Adapt<OrderDetailDto>(),
            "Order detail retrieved successfully.");
    }

    public async Task<ServiceResult<OrderDetailDto>> UpdateFinancialAdjustmentAsync(
        Guid orderId,
        Guid currentUserId,
        UpdateOrderFinancialAdjustmentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDetailDto>.Unauthorized();
        }

        var detail = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!OrderAccessEvaluator.CanManageFinancialAdjustment(role, detail.AssignedSalesId, currentUserId))
        {
            return ServiceResult<OrderDetailDto>.Forbidden(ForbiddenMessage);
        }

        if (detail.Status != OrderStatus.DEPOSIT_PENDING)
        {
            return BadRequestDetail(OrderErrorCodes.InvalidOrderStatus, InvalidOrderStatusMessage);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFoundDetail(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var depositPayment = await _payments.GetByOrderAndTypeAsync(
            orderId,
            PaymentType.DEPOSIT,
            cancellationToken);
        if (IsDepositPaymentStarted(depositPayment))
        {
            return BadRequestDetail(
                OrderErrorCodes.OrderPaymentAlreadyStarted,
                PaymentAlreadyStartedMessage);
        }

        var validation = ValidateFinancialAdjustment(order, request);
        if (validation is not null)
        {
            return validation;
        }

        var depositAmount = request.DepositAmount!.Value;
        var baseBeforeDiscount = OrderFinancialAdjustmentCalculator.CalculateBaseBeforeAdditionalDiscount(
            order.OriginalTotalAmount,
            order.ItemAdjustmentAmount ?? 0m);
        var additionalDiscountAmount = order.AdditionalDiscountAmount ?? 0m;
        var finalTotalAmount = OrderFinancialAdjustmentCalculator.CalculateFinalTotalAmount(
            baseBeforeDiscount,
            additionalDiscountAmount);

        order.DepositAmount = depositAmount;
        order.FinalTotalAmount = finalTotalAmount;

        var summedPaidAmount = await _payments.SumOrderScopedPaidAmountAsync(orderId, cancellationToken);
        var (paidAmount, remainingAmount) = OrderPaidAmountRecalculator.Calculate(
            finalTotalAmount,
            summedPaidAmount);
        order.PaidAmount = paidAmount;
        order.RemainingAmount = remainingAmount;
        order.UpdatedAt = DateTime.UtcNow;

        if (depositPayment is not null && CanSyncPendingDepositPayment(depositPayment))
        {
            SyncPendingDepositPayment(depositPayment, depositAmount, request.AdjustmentNote);
            _payments.UpdatePayment(depositPayment);
        }

        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedDetail = await _orders.GetDetailAsync(orderId, cancellationToken);
        return ServiceResult<OrderDetailDto>.Success(
            updatedDetail!.Adapt<OrderDetailDto>(),
            FinancialAdjustmentUpdatedMessage);
    }

    public async Task<ServiceResult<OrderAdjustmentDto>> CreateAdjustmentAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderAdjustmentDto request,
        CancellationToken cancellationToken = default)
    {
        var orderAccess = await ValidateOrderAdjustmentAccessAsync<OrderAdjustmentDto>(
            orderId,
            currentUserId,
            cancellationToken);
        if (orderAccess.Error is not null)
        {
            return orderAccess.Error;
        }

        if (orderAccess.Order!.Status != OrderStatus.IN_PRODUCTION)
        {
            return BadRequest<OrderAdjustmentDto>(
                OrderErrorCodes.OrderNotInProduction,
                OrderNotInProductionMessage);
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest<OrderAdjustmentDto>(
                OrderErrorCodes.InvalidAdjustment,
                "Adjustment reason is required.");
        }

        var now = DateTime.UtcNow;
        var adjustment = new OrderAdjustment
        {
            OrderAdjustmentId = Guid.NewGuid(),
            OrderId = orderId,
            Status = OrderAdjustmentStatus.DRAFT,
            Reason = reason,
            InternalNote = request.InternalNote?.Trim(),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        await _orders.AddAdjustmentAsync(adjustment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderAdjustmentDto>.Created(
            ToAdjustmentDto(adjustment),
            OrderAdjustmentCreatedMessage);
    }

    public async Task<ServiceResult<OrderAdjustmentItemDto>> AddAdjustmentItemAsync(
        Guid orderAdjustmentId,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default)
    {
        var context = await ValidateAdjustmentContextAsync<OrderAdjustmentItemDto>(
            orderAdjustmentId,
            currentUserId,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var itemResult = await BuildAdjustmentItemAsync(
            context.Adjustment!,
            currentUserId,
            request,
            cancellationToken);
        if (itemResult.Error is not null)
        {
            return itemResult.Error;
        }

        await _orders.AddAdjustmentItemAsync(itemResult.Item!, cancellationToken);
        await RecalculateAdjustmentTotalsAsync(context.Adjustment!, cancellationToken, includedItem: itemResult.Item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderAdjustmentItemDto>.Created(
            ToAdjustmentItemDto(itemResult.Item!),
            "Order adjustment item created successfully.");
    }

    public async Task<ServiceResult<OrderAdjustmentItemDto>> UpdateAdjustmentItemAsync(
        Guid orderAdjustmentItemId,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default)
    {
        var existingItem = await _orders.GetAdjustmentItemByIdAsync(orderAdjustmentItemId, cancellationToken);
        if (existingItem is null)
        {
            return NotFound<OrderAdjustmentItemDto>(
                OrderErrorCodes.OrderAdjustmentNotFound,
                OrderAdjustmentNotFoundMessage);
        }

        var context = await ValidateAdjustmentContextAsync<OrderAdjustmentItemDto>(
            existingItem.OrderAdjustmentId,
            currentUserId,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var itemResult = await BuildAdjustmentItemAsync(
            context.Adjustment!,
            currentUserId,
            request,
            cancellationToken);
        if (itemResult.Error is not null)
        {
            return itemResult.Error;
        }

        ApplyAdjustmentItem(existingItem, itemResult.Item!, currentUserId);
        _orders.UpdateAdjustmentItem(existingItem);
        await RecalculateAdjustmentTotalsAsync(context.Adjustment!, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderAdjustmentItemDto>.Success(
            ToAdjustmentItemDto(existingItem),
            OrderAdjustmentItemUpdatedMessage);
    }

    public async Task<ServiceResult<OrderAdjustmentDto>> DeleteAdjustmentItemAsync(
        Guid orderAdjustmentItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var item = await _orders.GetAdjustmentItemByIdAsync(orderAdjustmentItemId, cancellationToken);
        if (item is null)
        {
            return NotFound<OrderAdjustmentDto>(
                OrderErrorCodes.OrderAdjustmentNotFound,
                OrderAdjustmentNotFoundMessage);
        }

        var context = await ValidateAdjustmentContextAsync<OrderAdjustmentDto>(
            item.OrderAdjustmentId,
            currentUserId,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        _orders.RemoveAdjustmentItem(item);
        await RecalculateAdjustmentTotalsAsync(context.Adjustment!, cancellationToken, excludedItem: item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderAdjustmentDto>.Success(
            ToAdjustmentDto(context.Adjustment!),
            OrderAdjustmentItemDeletedMessage);
    }

    public async Task<ServiceResult<OrderAdjustmentConfirmationDto>> ConfirmAdjustmentAsync(
        Guid orderAdjustmentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderAdjustmentConfirmationDto>.Unauthorized();
        }

        var adjustment = await _orders.GetAdjustmentByIdAsync(orderAdjustmentId, cancellationToken);
        if (adjustment is null)
        {
            return NotFound<OrderAdjustmentConfirmationDto>(
                OrderErrorCodes.OrderAdjustmentNotFound,
                OrderAdjustmentNotFoundMessage);
        }

        var order = await _orders.GetDetailAsync(adjustment.OrderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderAdjustmentConfirmationDto>(
                OrderErrorCodes.OrderAdjustmentNotFound,
                OrderAdjustmentNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != "CUSTOMER" || order.CustomerId != currentUserId)
        {
            return ServiceResult<OrderAdjustmentConfirmationDto>.Forbidden(ForbiddenMessage);
        }

        if (adjustment.Status == OrderAdjustmentStatus.CONFIRMED)
        {
            return ServiceResult<OrderAdjustmentConfirmationDto>.Success(
                ToConfirmationDto(adjustment),
                "Order adjustment confirmed successfully.");
        }

        if (adjustment.Status != OrderAdjustmentStatus.DRAFT)
        {
            return BadRequest<OrderAdjustmentConfirmationDto>(
                OrderErrorCodes.InvalidAdjustmentStatus,
                "Order adjustment status is invalid for confirmation.");
        }

        var items = await _orders.GetAdjustmentItemsAsync(orderAdjustmentId, cancellationToken);
        if (items.Count == 0)
        {
            return BadRequest<OrderAdjustmentConfirmationDto>(
                OrderErrorCodes.AdjustmentItemRequired,
                "Order adjustment must contain at least one item.");
        }

        var now = DateTime.UtcNow;
        adjustment.Status = OrderAdjustmentStatus.CONFIRMED;
        adjustment.ConfirmedBy = currentUserId;
        adjustment.ConfirmedAt = now;
        adjustment.UpdatedBy = currentUserId;
        adjustment.UpdatedAt = now;
        _orders.UpdateAdjustment(adjustment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderAdjustmentConfirmationDto>.Success(
            ToConfirmationDto(adjustment),
            "Order adjustment confirmed successfully.");
    }

    public async Task<ServiceResult<OrderDeliveryStartDto>> StartDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDeliveryStartDto>.Unauthorized();
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderDeliveryStartDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<OrderDeliveryStartDto>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStartDelivery(role, project.AssignedSalesId, currentUserId))
        {
            return ServiceResult<OrderDeliveryStartDto>.Forbidden(ForbiddenMessage);
        }

        if (order.Status == OrderStatus.DELIVERING)
        {
            return ServiceResult<OrderDeliveryStartDto>.Success(
                ToDeliveryStartDto(order, project),
                "Delivery started successfully.");
        }

        if (order.Status != OrderStatus.READY_FOR_DELIVERY)
        {
            return BadRequest<OrderDeliveryStartDto>(
                OrderErrorCodes.InvalidOrderStatus,
                "Order must be READY_FOR_DELIVERY before delivery can start.");
        }

        if (!await _schedules.HasConfirmedDeliveryScheduleAsync(order.ProjectId, cancellationToken))
        {
            return BadRequest<OrderDeliveryStartDto>(
                OrderErrorCodes.DeliveryScheduleNotConfirmed,
                "At least one delivery schedule must be confirmed before delivery can start.");
        }

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.DELIVERING;
        order.UpdatedAt = now;
        project.Status = ProjectStatus.DELIVERING;
        project.UpdatedAt = now;

        _orders.Update(order);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderDeliveryStartDto>.Success(
            ToDeliveryStartDto(order, project),
            "Delivery started successfully.");
    }

    public async Task<ServiceResult<OrderItemDeliveredQuantityDto>> UpdateDeliveredQuantityAsync(
        Guid orderItemId,
        Guid currentUserId,
        UpdateDeliveredQuantityRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderItemDeliveredQuantityDto>.Unauthorized();
        }

        var increment = request.DeliveredQuantityIncrement ?? 0;
        if (increment <= 0)
        {
            return BadRequest<OrderItemDeliveredQuantityDto>(
                OrderErrorCodes.InvalidDeliveredQuantity,
                "Delivered quantity increment must be greater than zero.");
        }

        var context = await ValidateOrderItemDeliveryContextAsync<OrderItemDeliveredQuantityDto>(
            orderItemId,
            currentUserId,
            requireCustomerOwner: false,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var readyError = ValidateReadyOrderItem<OrderItemDeliveredQuantityDto>(context.Item!);
        if (readyError is not null)
        {
            return readyError;
        }

        var quantity = context.Item!.Quantity ?? 0;
        var deliveredQuantity = context.Item.DeliveredQuantity ?? 0;
        if (quantity <= 0)
        {
            return BadRequest<OrderItemDeliveredQuantityDto>(
                OrderErrorCodes.InvalidDeliveredQuantity,
                "Order item quantity must be greater than zero.");
        }

        if (deliveredQuantity + increment > quantity)
        {
            return BadRequest<OrderItemDeliveredQuantityDto>(
                OrderErrorCodes.DeliveredQuantityExceeded,
                "Delivered quantity cannot exceed ordered quantity.");
        }

        var now = DateTime.UtcNow;
        var updatedItem = await _orders.TryIncrementDeliveredQuantityAsync(
            context.Item.OrderItemId,
            increment,
            request.DeliveryNote?.Trim(),
            currentUserId,
            now,
            cancellationToken);
        if (updatedItem is null)
        {
            return BadRequest<OrderItemDeliveredQuantityDto>(
                OrderErrorCodes.DeliveredQuantityExceeded,
                "Delivered quantity cannot exceed ordered quantity.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderItemDeliveredQuantityDto>.Success(
            ToDeliveredQuantityDto(updatedItem),
            "Delivered quantity updated successfully.");
    }

    public async Task<ServiceResult<OrderItemDeliveryConfirmationDto>> ConfirmItemDeliveryAsync(
        Guid orderItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderItemDeliveryConfirmationDto>.Unauthorized();
        }

        var context = await ValidateOrderItemDeliveryContextAsync<OrderItemDeliveryConfirmationDto>(
            orderItemId,
            currentUserId,
            requireCustomerOwner: true,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        if (context.Item!.Status == OrderItemStatus.DELIVERED)
        {
            return ServiceResult<OrderItemDeliveryConfirmationDto>.Success(
                ToDeliveryConfirmationDto(context.Item, context.Order!),
                "Order item delivery confirmed successfully.");
        }

        var readyError = ValidateReadyOrderItem<OrderItemDeliveryConfirmationDto>(context.Item);
        if (readyError is not null)
        {
            return readyError;
        }

        if (!IsFullyDelivered(context.Item))
        {
            return BadRequest<OrderItemDeliveryConfirmationDto>(
                OrderErrorCodes.ItemNotFullyDelivered,
                "Order item must be fully delivered before confirmation.");
        }

        var transitionError = OrderItemStatusTransitionService.Validate(
            context.Item.Status,
            OrderItemStatus.DELIVERED,
            OrderItemStatusTransitionOwner.CustomerDeliveryConfirmation);
        if (transitionError is not null)
        {
            return BadRequest<OrderItemDeliveryConfirmationDto>(
                transitionError.ErrorCode,
                transitionError.Message);
        }

        var now = DateTime.UtcNow;
        context.Item.Status = OrderItemStatus.DELIVERED;
        context.Item.CustomerConfirmedAt = now;
        _orders.UpdateItem(context.Item);

        if (await AllDeliverableItemsConfirmedAsync(context.Item, cancellationToken))
        {
            context.Order!.Status = OrderStatus.DELIVERED;
            context.Order.CustomerConfirmedDeliveryAt = now;
            context.Order.UpdatedAt = now;
            context.Project!.Status = ProjectStatus.DELIVERED;
            context.Project.UpdatedAt = now;
            _orders.Update(context.Order);
            _projects.Update(context.Project);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderItemDeliveryConfirmationDto>.Success(
            ToDeliveryConfirmationDto(context.Item, context.Order!),
            "Order item delivery confirmed successfully.");
    }

    public async Task<ServiceResult<OrderFinalPaymentPreparationDto>> PrepareFinalPaymentAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderFinalPaymentPreparationDto>.Unauthorized();
        }

        var detail = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (detail is null)
        {
            return NotFound<OrderFinalPaymentPreparationDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanManageAsAssignedSales(
                role,
                detail.AssignedSalesId,
                currentUserId))
        {
            return ServiceResult<OrderFinalPaymentPreparationDto>.Forbidden(ForbiddenMessage);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderFinalPaymentPreparationDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        if (order.Status != OrderStatus.DELIVERED)
        {
            return BadRequest<OrderFinalPaymentPreparationDto>(
                OrderErrorCodes.OrderNotDelivered,
                "Order must be DELIVERED before final payment preparation.");
        }

        if (!order.CustomerConfirmedDeliveryAt.HasValue)
        {
            return BadRequest<OrderFinalPaymentPreparationDto>(
                OrderErrorCodes.DeliveryNotConfirmed,
                "Customer delivery confirmation is required before final payment preparation.");
        }

        var adjustments = await _orders.GetAdjustmentsByOrderAsync(orderId, cancellationToken);
        if (adjustments.Any(adjustment => adjustment.Status is not (OrderAdjustmentStatus.APPLIED or OrderAdjustmentStatus.CANCELLED)))
        {
            return BadRequest<OrderFinalPaymentPreparationDto>(
                OrderErrorCodes.AdjustmentNotApplied,
                "All order adjustments must be applied or cancelled before final payment preparation.");
        }

        ApplyFinalPaymentFinancialSummary(
            order,
            adjustments.Where(adjustment => adjustment.Status == OrderAdjustmentStatus.APPLIED));

        var paidAmount = await _payments.SumOrderScopedPaidAmountAsync(orderId, cancellationToken);
        var remainingAmount = order.FinalTotalAmount - Math.Max(0m, paidAmount);
        if (remainingAmount < 0m)
        {
            return BadRequest<OrderFinalPaymentPreparationDto>(
                OrderErrorCodes.NegativeRemainingAmount,
                "Remaining amount must not be negative.");
        }

        order.PaidAmount = Math.Max(0m, paidAmount);
        order.RemainingAmount = remainingAmount;
        order.Status = remainingAmount > 0m
            ? OrderStatus.FINAL_PAYMENT_PENDING
            : OrderStatus.DELIVERED;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requiresRemainingPayment = remainingAmount > 0m;
        var message = requiresRemainingPayment
            ? "Order is ready for remaining payment."
            : "No remaining payment is required.";
        return ServiceResult<OrderFinalPaymentPreparationDto>.Success(
            ToFinalPaymentPreparationDto(order, requiresRemainingPayment),
            message);
    }

    public async Task<ServiceResult<OrderCompletionDto>> CompleteAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderCompletionDto>.Unauthorized();
        }

        var detail = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (detail is null)
        {
            return NotFound<OrderCompletionDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanManageAsAssignedSales(
                role,
                detail.AssignedSalesId,
                currentUserId))
        {
            return ServiceResult<OrderCompletionDto>.Forbidden(ForbiddenMessage);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderCompletionDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<OrderCompletionDto>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        if (order.Status == OrderStatus.COMPLETED)
        {
            return ServiceResult<OrderCompletionDto>.Success(
                ToCompletionDto(order, project, order.UpdatedAt ?? DateTime.UtcNow),
                "Order and project completed successfully.");
        }

        var validationError = await ValidateCompletionReadinessAsync<OrderCompletionDto>(
            order,
            cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.COMPLETED;
        order.UpdatedAt = now;
        project.Status = ProjectStatus.COMPLETED;
        project.UpdatedAt = now;
        _orders.Update(order);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderCompletionDto>.Success(
            ToCompletionDto(order, project, now),
            "Order and project completed successfully.");
    }

    private async Task<OrderAdjustmentAccess<T>> ValidateOrderAdjustmentAccessAsync<T>(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return new OrderAdjustmentAccess<T>(null, ServiceResult<T>.Unauthorized());
        }

        var detail = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (detail is null)
        {
            return new OrderAdjustmentAccess<T>(
                null,
                NotFound<T>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!OrderAccessEvaluator.CanManageFinancialAdjustment(role, detail.AssignedSalesId, currentUserId))
        {
            return new OrderAdjustmentAccess<T>(null, ServiceResult<T>.Forbidden(ForbiddenMessage));
        }

        return new OrderAdjustmentAccess<T>(detail, null);
    }

    private async Task<OrderAdjustmentContext<T>> ValidateAdjustmentContextAsync<T>(
        Guid orderAdjustmentId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var adjustment = await _orders.GetAdjustmentByIdAsync(orderAdjustmentId, cancellationToken);
        if (adjustment is null)
        {
            return new OrderAdjustmentContext<T>(
                null,
                NotFound<T>(OrderErrorCodes.OrderAdjustmentNotFound, OrderAdjustmentNotFoundMessage));
        }

        var access = await ValidateOrderAdjustmentAccessAsync<T>(
            adjustment.OrderId,
            currentUserId,
            cancellationToken);
        if (access.Error is not null)
        {
            return new OrderAdjustmentContext<T>(null, access.Error);
        }

        if (adjustment.Status != OrderAdjustmentStatus.DRAFT)
        {
            return new OrderAdjustmentContext<T>(
                null,
                BadRequest<T>(
                    OrderErrorCodes.AdjustmentAlreadyConfirmed,
                    "Order adjustment is already confirmed."));
        }

        return new OrderAdjustmentContext<T>(adjustment, null);
    }

    private async Task<OrderAdjustmentItemBuildResult> BuildAdjustmentItemAsync(
        OrderAdjustment adjustment,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken)
    {
        var commonValidation = ValidateAdjustmentItemRequest(request);
        if (commonValidation is not null)
        {
            return new OrderAdjustmentItemBuildResult(null, commonValidation);
        }

        return request.AdjustmentType!.Value switch
        {
            OrderAdjustmentItemType.UNAVAILABLE_ITEM => await BuildUnavailableItemAsync(
                adjustment,
                currentUserId,
                request,
                cancellationToken),
            OrderAdjustmentItemType.ADDITIONAL_DISCOUNT => BuildAdditionalDiscountItem(
                adjustment.OrderAdjustmentId,
                currentUserId,
                request),
            _ => new OrderAdjustmentItemBuildResult(null, InvalidAdjustmentItemResult())
        };
    }

    private async Task<OrderAdjustmentItemBuildResult> BuildUnavailableItemAsync(
        OrderAdjustment adjustment,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken)
    {
        if (!request.OrderItemId.HasValue)
        {
            return new OrderAdjustmentItemBuildResult(null, InvalidAdjustmentItemResult());
        }

        var orderItem = await _orders.GetItemByIdAsync(request.OrderItemId.Value, cancellationToken);
        if (orderItem is null || orderItem.OrderId != adjustment.OrderId)
        {
            return new OrderAdjustmentItemBuildResult(
                null,
                NotFound<OrderAdjustmentItemDto>(OrderErrorCodes.OrderItemNotFound, OrderItemNotFoundMessage));
        }

        var order = await _orders.GetByIdAsync(adjustment.OrderId, cancellationToken);
        if (order is null)
        {
            return new OrderAdjustmentItemBuildResult(
                null,
                NotFound<OrderAdjustmentItemDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage));
        }

        var itemTotalAmount = OrderFinancialAdjustmentCalculator.CalculateVatInclusiveUnavailableAdjustment(
            orderItem.SubtotalAmount ?? 0m,
            order.VatRate);
        if (request.AdjustmentAmount.HasValue && request.AdjustmentAmount.Value != itemTotalAmount)
        {
            return new OrderAdjustmentItemBuildResult(
                null,
                BadRequest<OrderAdjustmentItemDto>(
                    OrderErrorCodes.InvalidUnavailableItemAmount,
                    "Unavailable item adjustment amount must equal order item total amount."));
        }

        if (!await _orders.HasCancelledProductionItemAsync(orderItem.OrderItemId, cancellationToken))
        {
            return new OrderAdjustmentItemBuildResult(
                null,
                BadRequest<OrderAdjustmentItemDto>(
                    OrderErrorCodes.ProductionItemNotCancelled,
                    "Related production item must be cancelled."));
        }

        return new OrderAdjustmentItemBuildResult(
            CreateAdjustmentItem(
                adjustment.OrderAdjustmentId,
                currentUserId,
                OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                orderItem.OrderItemId,
                itemTotalAmount,
                itemTotalAmount,
                request.Reason!),
            null);
    }

    private static OrderAdjustmentItemBuildResult BuildAdditionalDiscountItem(
        Guid orderAdjustmentId,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request)
    {
        if (request.AdjustmentAmount is null or <= 0m)
        {
            return new OrderAdjustmentItemBuildResult(null, InvalidAdjustmentItemResult());
        }

        return new OrderAdjustmentItemBuildResult(
            CreateAdjustmentItem(
                orderAdjustmentId,
                currentUserId,
                OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                null,
                0m,
                request.AdjustmentAmount.Value,
                request.Reason!),
            null);
    }

    private async Task RecalculateAdjustmentTotalsAsync(
        OrderAdjustment adjustment,
        CancellationToken cancellationToken,
        OrderAdjustmentItem? excludedItem = null,
        OrderAdjustmentItem? includedItem = null)
    {
        var items = (await _orders.GetAdjustmentItemsAsync(
                adjustment.OrderAdjustmentId,
                cancellationToken))
            .Where(item => excludedItem is null ||
                item.OrderAdjustmentItemId != excludedItem.OrderAdjustmentItemId)
            .ToList();

        if (includedItem is not null &&
            items.All(item => item.OrderAdjustmentItemId != includedItem.OrderAdjustmentItemId))
        {
            items.Add(includedItem);
        }

        adjustment.ItemAdjustmentAmount = SumAdjustmentItems(items, OrderAdjustmentItemType.UNAVAILABLE_ITEM);
        adjustment.AdditionalDiscountAmount = SumAdjustmentItems(items, OrderAdjustmentItemType.ADDITIONAL_DISCOUNT);
        adjustment.TotalAdjustmentAmount = adjustment.ItemAdjustmentAmount + adjustment.AdditionalDiscountAmount;
        adjustment.UpdatedAt = DateTime.UtcNow;
        _orders.UpdateAdjustment(adjustment);
    }

    private static ServiceResult<OrderAdjustmentItemDto>? ValidateAdjustmentItemRequest(
        UpsertOrderAdjustmentItemDto request)
    {
        if (!request.AdjustmentType.HasValue || string.IsNullOrWhiteSpace(request.Reason))
        {
            return InvalidAdjustmentItemResult();
        }

        return null;
    }

    private static void ApplyAdjustmentItem(
        OrderAdjustmentItem existingItem,
        OrderAdjustmentItem source,
        Guid currentUserId)
    {
        existingItem.OrderItemId = source.OrderItemId;
        existingItem.AdjustmentType = source.AdjustmentType;
        existingItem.PreviousItemAmount = source.PreviousItemAmount;
        existingItem.AdjustmentAmount = source.AdjustmentAmount;
        existingItem.Reason = source.Reason;
        existingItem.UpdatedBy = currentUserId;
        existingItem.UpdatedAt = DateTime.UtcNow;
    }

    private static OrderAdjustmentItem CreateAdjustmentItem(
        Guid orderAdjustmentId,
        Guid currentUserId,
        OrderAdjustmentItemType adjustmentType,
        Guid? orderItemId,
        decimal previousItemAmount,
        decimal adjustmentAmount,
        string reason)
    {
        return new OrderAdjustmentItem
        {
            OrderAdjustmentItemId = Guid.NewGuid(),
            OrderAdjustmentId = orderAdjustmentId,
            OrderItemId = orderItemId,
            AdjustmentType = adjustmentType,
            PreviousItemAmount = previousItemAmount,
            AdjustmentAmount = adjustmentAmount,
            Reason = reason.Trim(),
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static decimal SumAdjustmentItems(
        IEnumerable<OrderAdjustmentItem> items,
        OrderAdjustmentItemType adjustmentType)
    {
        return items
            .Where(item => item.AdjustmentType == adjustmentType)
            .Sum(item => item.AdjustmentAmount);
    }

    private static bool IsDepositPaymentStarted(Payment? depositPayment)
    {
        if (depositPayment is null)
        {
            return false;
        }

        if (depositPayment.Status is PaymentStatus.PROCESSING or PaymentStatus.PAID)
        {
            return true;
        }

        return false;
    }

    private static bool CanSyncPendingDepositPayment(Payment depositPayment)
    {
        return depositPayment.Status == PaymentStatus.PENDING;
    }

    private static void SyncPendingDepositPayment(
        Payment depositPayment,
        decimal depositAmount,
        string? adjustmentNote)
    {
        depositPayment.Amount = depositAmount;
        if (!string.IsNullOrWhiteSpace(adjustmentNote))
        {
            depositPayment.Note = adjustmentNote.Trim();
        }

        depositPayment.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyFinalPaymentFinancialSummary(
        Order order,
        IEnumerable<OrderAdjustment> appliedAdjustments)
    {
        var applied = appliedAdjustments.ToList();
        var itemAdjustmentAmount = applied.Sum(adjustment => adjustment.ItemAdjustmentAmount);
        var additionalDiscountAmount = applied.Sum(adjustment => adjustment.AdditionalDiscountAmount);
        var baseBeforeDiscount = OrderFinancialAdjustmentCalculator.CalculateBaseBeforeAdditionalDiscount(
            order.OriginalTotalAmount,
            itemAdjustmentAmount);

        order.ItemAdjustmentAmount = itemAdjustmentAmount;
        order.AdditionalDiscountAmount = additionalDiscountAmount;
        order.FinalTotalAmount = OrderFinancialAdjustmentCalculator.CalculateFinalTotalAmount(
            baseBeforeDiscount,
            additionalDiscountAmount);
    }

    private async Task<ServiceResult<T>?> ValidateCompletionReadinessAsync<T>(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.Status is not (OrderStatus.DELIVERED or OrderStatus.FINAL_PAYMENT_PENDING))
        {
            return BadRequest<T>(
                OrderErrorCodes.OrderNotReadyToComplete,
                "Order is not ready to complete.");
        }

        if (!order.CustomerConfirmedDeliveryAt.HasValue)
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryNotCompleted,
                "Delivery must be fully confirmed before order completion.");
        }

        var items = await _orders.GetItemsByOrderAsync(order.OrderId, cancellationToken);
        if (items.Where(IsActiveDeliveryItem).Any(item => item.Status != OrderItemStatus.DELIVERED))
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryNotCompleted,
                "Delivery must be fully confirmed before order completion.");
        }

        var adjustments = await _orders.GetAdjustmentsByOrderAsync(order.OrderId, cancellationToken);
        if (adjustments.Any(adjustment => adjustment.Status is not (OrderAdjustmentStatus.APPLIED or OrderAdjustmentStatus.CANCELLED)))
        {
            return BadRequest<T>(
                OrderErrorCodes.AdjustmentNotApplied,
                "All order adjustments must be applied or cancelled before order completion.");
        }

        var paidAmount = await _payments.SumOrderScopedPaidAmountAsync(order.OrderId, cancellationToken);
        var (recalculatedPaidAmount, remainingAmount) = OrderPaidAmountRecalculator.Calculate(
            order.FinalTotalAmount,
            paidAmount);
        if (remainingAmount > 0m)
        {
            return BadRequest<T>(
                OrderErrorCodes.RemainingPaymentNotPaid,
                "Remaining payment must be paid before order completion.");
        }

        order.PaidAmount = recalculatedPaidAmount;
        order.RemainingAmount = remainingAmount;
        return null;
    }

    private static ServiceResult<OrderDetailDto>? ValidateFinancialAdjustment(
        Order order,
        UpdateOrderFinancialAdjustmentRequestDto request)
    {
        if (request.AdditionalDiscountAmount.HasValue)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Additional discount amount must be updated through order adjustment flow.");
        }

        if (!request.DepositAmount.HasValue)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Deposit amount is required.");
        }

        var depositAmount = request.DepositAmount.Value;
        var baseBeforeDiscount = OrderFinancialAdjustmentCalculator.CalculateBaseBeforeAdditionalDiscount(
            order.OriginalTotalAmount,
            order.ItemAdjustmentAmount ?? 0m);
        var finalTotalAmount = OrderFinancialAdjustmentCalculator.CalculateFinalTotalAmount(
            baseBeforeDiscount,
            order.AdditionalDiscountAmount ?? 0m);
        if (depositAmount <= 0m)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Deposit amount must be greater than zero.");
        }

        if (depositAmount > finalTotalAmount)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Deposit amount must not exceed the final total amount.");
        }

        return null;
    }

    private static List<OrderListItemReadModel> FilterByAccess(
        IReadOnlyList<OrderListItemReadModel> items,
        string? role,
        Guid currentUserId)
    {
        return items
            .Where(item => OrderAccessEvaluator.CanViewOrder(
                role,
                item.CustomerId,
                item.AssignedSalesId,
                item.AssignedDesignerId,
                currentUserId,
                item.Status))
            .ToList();
    }

    private async Task<OrderItemDeliveryContext<T>> ValidateOrderItemDeliveryContextAsync<T>(
        Guid orderItemId,
        Guid currentUserId,
        bool requireCustomerOwner,
        CancellationToken cancellationToken)
    {
        var item = await _orders.GetItemByIdAsync(orderItemId, cancellationToken);
        if (item is null)
        {
            return OrderItemDeliveryContext<T>.WithError(
                NotFound<T>(OrderErrorCodes.OrderItemNotFound, "Order item not found."));
        }

        var order = await _orders.GetByIdAsync(item.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderItemDeliveryContext<T>.WithError(
                NotFound<T>(OrderErrorCodes.OrderItemNotFound, "Order item not found."));
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return OrderItemDeliveryContext<T>.WithError(
                NotFound<T>(OrderErrorCodes.OrderItemNotFound, "Order item not found."));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var accessError = requireCustomerOwner
            ? ValidateCustomerDeliveryAccess<T>(role, project.CustomerId, currentUserId)
            : ValidateStaffDeliveryAccess<T>(role, project.AssignedSalesId, currentUserId);
        if (accessError is not null)
        {
            return OrderItemDeliveryContext<T>.WithError(accessError);
        }

        if (order.Status != OrderStatus.DELIVERING)
        {
            return OrderItemDeliveryContext<T>.WithError(
                BadRequest<T>(
                    OrderErrorCodes.OrderNotDelivering,
                    "Order must be DELIVERING before item delivery can be updated."));
        }

        if (!IsProductLineItem(item))
        {
            return OrderItemDeliveryContext<T>.WithError(
                BadRequest<T>(
                    OrderErrorCodes.ItemNotDeliverable,
                    "Order item is not deliverable."));
        }

        return new OrderItemDeliveryContext<T>(item, order, project, null);
    }

    private static ServiceResult<T>? ValidateStaffDeliveryAccess<T>(
        string? role,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return CanStartDelivery(role, assignedSalesId, currentUserId)
            ? null
            : ServiceResult<T>.Forbidden(ForbiddenMessage);
    }

    private static ServiceResult<T>? ValidateCustomerDeliveryAccess<T>(
        string? role,
        Guid customerId,
        Guid currentUserId)
    {
        return role == ProjectAssignmentAccessEvaluator.CustomerRole && customerId == currentUserId
            ? null
            : ServiceResult<T>.Forbidden(ForbiddenMessage);
    }

    private static bool IsProductLineItem(OrderItem item)
    {
        return item.ProductVersionId.HasValue &&
            (item.Quantity ?? 0) > 0 &&
            item.Status is not (OrderItemStatus.UNAVAILABLE or OrderItemStatus.CANCELLED);
    }

    private static bool IsActiveDeliveryItem(OrderItem item)
    {
        return IsProductLineItem(item) &&
            item.Status is OrderItemStatus.READY or OrderItemStatus.DELIVERED;
    }

    private static ServiceResult<T>? ValidateReadyOrderItem<T>(OrderItem item)
    {
        return item.Status == OrderItemStatus.READY
            ? null
            : BadRequest<T>(
                OrderErrorCodes.OrderItemNotReady,
                "Order item must be READY before delivery can be updated.");
    }

    private static bool IsFullyDelivered(OrderItem item)
    {
        return (item.DeliveredQuantity ?? 0) >= (item.Quantity ?? 0) && (item.Quantity ?? 0) > 0;
    }

    private async Task<bool> AllDeliverableItemsConfirmedAsync(
        OrderItem currentItem,
        CancellationToken cancellationToken)
    {
        var items = await _orders.GetItemsByOrderAsync(currentItem.OrderId, cancellationToken);
        return items
            .Where(IsActiveDeliveryItem)
            .All(item =>
                item.OrderItemId == currentItem.OrderItemId ||
                item.Status == OrderItemStatus.DELIVERED);
    }

    private static ServiceResult<OrderListResponseDto> NotFoundList(string errorCode, string message)
    {
        return ServiceResult<OrderListResponseDto>.Failure(Error.NotFound(errorCode, message));
    }

    private static ServiceResult<OrderDetailDto> NotFoundDetail(string errorCode, string message)
    {
        return ServiceResult<OrderDetailDto>.Failure(Error.NotFound(errorCode, message));
    }

    private static ServiceResult<OrderDetailDto> BadRequestDetail(string errorCode, string message)
    {
        return ServiceResult<OrderDetailDto>.Failure(Error.BadRequest(errorCode, message));
    }

    private static OrderAdjustmentDto ToAdjustmentDto(OrderAdjustment adjustment)
    {
        return new OrderAdjustmentDto
        {
            OrderAdjustmentId = adjustment.OrderAdjustmentId,
            OrderId = adjustment.OrderId,
            Status = adjustment.Status.ToString(),
            ItemAdjustmentAmount = adjustment.ItemAdjustmentAmount,
            AdditionalDiscountAmount = adjustment.AdditionalDiscountAmount,
            TotalAdjustmentAmount = adjustment.TotalAdjustmentAmount
        };
    }

    private static OrderAdjustmentItemDto ToAdjustmentItemDto(OrderAdjustmentItem item)
    {
        return new OrderAdjustmentItemDto
        {
            OrderAdjustmentItemId = item.OrderAdjustmentItemId,
            OrderAdjustmentId = item.OrderAdjustmentId,
            OrderItemId = item.OrderItemId,
            AdjustmentType = item.AdjustmentType.ToString(),
            PreviousItemAmount = item.PreviousItemAmount,
            AdjustmentAmount = item.AdjustmentAmount,
            ItemTotalAmount = item.AdjustmentType == OrderAdjustmentItemType.UNAVAILABLE_ITEM
                ? item.PreviousItemAmount
                : null,
            Reason = item.Reason
        };
    }

    private static OrderAdjustmentConfirmationDto ToConfirmationDto(OrderAdjustment adjustment)
    {
        return new OrderAdjustmentConfirmationDto
        {
            OrderAdjustmentId = adjustment.OrderAdjustmentId,
            Status = adjustment.Status.ToString(),
            ConfirmedBy = adjustment.ConfirmedBy,
            ConfirmedAt = adjustment.ConfirmedAt
        };
    }

    private static OrderDeliveryStartDto ToDeliveryStartDto(Order order, Project project)
    {
        return new OrderDeliveryStartDto
        {
            OrderId = order.OrderId,
            ProjectId = project.ProjectId,
            OrderStatus = order.Status.ToString() ?? string.Empty,
            ProjectStatus = project.Status.ToString() ?? string.Empty,
            UpdatedAt = order.UpdatedAt
        };
    }

    private static OrderItemDeliveredQuantityDto ToDeliveredQuantityDto(OrderItem item)
    {
        return new OrderItemDeliveredQuantityDto
        {
            OrderItemId = item.OrderItemId,
            Quantity = item.Quantity ?? 0,
            DeliveredQuantity = item.DeliveredQuantity ?? 0,
            LastDeliveredAt = item.LastDeliveredAt,
            LastDeliveredBy = item.LastDeliveredBy
        };
    }

    private static OrderItemDeliveryConfirmationDto ToDeliveryConfirmationDto(OrderItem item, Order order)
    {
        return new OrderItemDeliveryConfirmationDto
        {
            OrderItemId = item.OrderItemId,
            Status = item.Status.ToString() ?? string.Empty,
            CustomerConfirmedAt = item.CustomerConfirmedAt,
            OrderStatus = order.Status.ToString() ?? string.Empty
        };
    }

    private static OrderFinalPaymentPreparationDto ToFinalPaymentPreparationDto(
        Order order,
        bool requiresRemainingPayment)
    {
        return new OrderFinalPaymentPreparationDto
        {
            OrderId = order.OrderId,
            Status = order.Status?.ToString() ?? string.Empty,
            FinalTotalAmount = order.FinalTotalAmount,
            PaidAmount = order.PaidAmount ?? 0m,
            RemainingAmount = order.RemainingAmount ?? 0m,
            RequiresRemainingPayment = requiresRemainingPayment
        };
    }

    private static OrderCompletionDto ToCompletionDto(
        Order order,
        Project project,
        DateTime completedAt)
    {
        return new OrderCompletionDto
        {
            OrderId = order.OrderId,
            OrderStatus = order.Status?.ToString() ?? string.Empty,
            ProjectId = project.ProjectId,
            ProjectStatus = project.Status?.ToString() ?? string.Empty,
            CompletedAt = completedAt
        };
    }

    private static bool CanStartDelivery(
        string? role,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return role is ProjectAssignmentAccessEvaluator.AdminRole or OrderAccessEvaluator.ProductionRole ||
            role == ProjectAssignmentAccessEvaluator.SalesRole && assignedSalesId == currentUserId;
    }

    private static ServiceResult<T> BadRequest<T>(string errorCode, string message)
    {
        return ServiceResult<T>.Failure(Error.BadRequest(errorCode, message));
    }

    private static ServiceResult<T> NotFound<T>(string errorCode, string message)
    {
        return ServiceResult<T>.Failure(Error.NotFound(errorCode, message));
    }

    private static ServiceResult<OrderAdjustmentItemDto> InvalidAdjustmentItemResult()
    {
        return BadRequest<OrderAdjustmentItemDto>(
            OrderErrorCodes.InvalidAdjustmentItem,
            "Order adjustment item is invalid.");
    }

    private sealed record OrderAdjustmentAccess<T>(
        OrderDetailReadModel? Order,
        ServiceResult<T>? Error);

    private sealed record OrderAdjustmentContext<T>(
        OrderAdjustment? Adjustment,
        ServiceResult<T>? Error);

    private sealed record OrderAdjustmentItemBuildResult(
        OrderAdjustmentItem? Item,
        ServiceResult<OrderAdjustmentItemDto>? Error);

    private sealed record OrderItemDeliveryContext<T>(
        OrderItem? Item,
        Order? Order,
        Project? Project,
        ServiceResult<T>? Error)
    {
        public static OrderItemDeliveryContext<T> WithError(ServiceResult<T> error)
        {
            return new OrderItemDeliveryContext<T>(null, null, null, error);
        }
    }
}
