using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
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
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orders,
        IProjectRepository projects,
        IPaymentRepository payments,
        IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _projects = projects;
        _payments = payments;
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

        var additionalDiscountAmount = request.AdditionalDiscountAmount!.Value;
        var depositAmount = request.DepositAmount!.Value;
        var baseBeforeDiscount = OrderFinancialAdjustmentCalculator.CalculateBaseBeforeAdditionalDiscount(
            order.OriginalTotalAmount,
            order.ItemAdjustmentAmount ?? 0m);
        var finalTotalAmount = OrderFinancialAdjustmentCalculator.CalculateFinalTotalAmount(
            baseBeforeDiscount,
            additionalDiscountAmount);

        order.AdditionalDiscountAmount = additionalDiscountAmount;
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

        var subtotal = orderItem.SubtotalAmount ?? 0m;
        if (request.AdjustmentAmount.HasValue && request.AdjustmentAmount.Value != subtotal)
        {
            return new OrderAdjustmentItemBuildResult(
                null,
                BadRequest<OrderAdjustmentItemDto>(
                    OrderErrorCodes.InvalidUnavailableItemAmount,
                    "Unavailable item adjustment amount must equal order item subtotal amount."));
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
                subtotal,
                subtotal,
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

    private static ServiceResult<OrderDetailDto>? ValidateFinancialAdjustment(
        Order order,
        UpdateOrderFinancialAdjustmentRequestDto request)
    {
        if (!request.AdditionalDiscountAmount.HasValue)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Additional discount amount is required.");
        }

        if (!request.DepositAmount.HasValue)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Deposit amount is required.");
        }

        var additionalDiscountAmount = request.AdditionalDiscountAmount.Value;
        var depositAmount = request.DepositAmount.Value;
        var baseBeforeDiscount = OrderFinancialAdjustmentCalculator.CalculateBaseBeforeAdditionalDiscount(
            order.OriginalTotalAmount,
            order.ItemAdjustmentAmount ?? 0m);

        if (additionalDiscountAmount < 0m)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Additional discount amount must be greater than or equal to zero.");
        }

        if (additionalDiscountAmount >= baseBeforeDiscount)
        {
            return BadRequestDetail(
                OrderErrorCodes.InvalidFinancialAdjustment,
                "Additional discount amount must be less than the order total before discount.");
        }

        var finalTotalAmount = OrderFinancialAdjustmentCalculator.CalculateFinalTotalAmount(
            baseBeforeDiscount,
            additionalDiscountAmount);
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
            Reason = item.Reason
        };
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
}
