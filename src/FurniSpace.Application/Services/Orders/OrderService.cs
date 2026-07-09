using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
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
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string OrderNotFoundMessage = "Order not found.";
    private const string ForbiddenMessage = "You do not have permission to update this order financial adjustment.";
    private const string InvalidOrderStatusMessage = "Order is not pending deposit payment.";
    private const string PaymentAlreadyStartedMessage =
        "Order deposit payment has already started and cannot be adjusted.";
    private const string FinancialAdjustmentUpdatedMessage = "Order financial adjustment updated successfully.";

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

    private static bool IsDepositPaymentStarted(Payment? depositPayment)
    {
        if (depositPayment is null)
        {
            return false;
        }

        if (depositPayment.Status is PaymentStatus.PARTIALLY_PAID or PaymentStatus.PAID)
        {
            return true;
        }

        return depositPayment.PaidAmount > 0m;
    }

    private static bool CanSyncPendingDepositPayment(Payment depositPayment)
    {
        return depositPayment.Status == PaymentStatus.PENDING
            && depositPayment.PaidAmount <= 0m;
    }

    private static void SyncPendingDepositPayment(
        Payment depositPayment,
        decimal depositAmount,
        string? adjustmentNote)
    {
        depositPayment.Amount = depositAmount;
        depositPayment.RemainingAmount = depositAmount;
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
}
