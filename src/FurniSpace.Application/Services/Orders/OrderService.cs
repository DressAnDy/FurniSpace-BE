using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Common.Projects;
using static FurniSpace.Application.Constants.Orders.OrderServiceConstants;
using FurniSpace.Application.Constants.Payments;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Orders;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IProjectRepository _projects;
    private readonly IPaymentRepository _payments;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IProjectScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SePayOptions _sePayOptions;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger? _logger;
    private const int RemainingPaymentExpiryDays = 7;

    public OrderService(
        IOrderRepository orders,
        IProjectRepository projects,
        IPaymentRepository payments,
        OrderServiceDependencies dependencies)
    {
        _orders = orders;
        _projects = projects;
        _payments = payments;
        _productionRequests = dependencies.ProductionRequests;
        _schedules = dependencies.Schedules;
        _unitOfWork = dependencies.UnitOfWork;
        _sePayOptions = dependencies.SePayOptions;
        _notifications = dependencies.Notifications;
        _logger = dependencies.Logger;
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

        if (order.Status == OrderStatus.DELIVERED ||
            order.Status == OrderStatus.FINAL_PAYMENT_PENDING ||
            order.Status == OrderStatus.COMPLETED)
        {
            return ServiceResult<OrderDeliveryStartDto>.Failure(
                Error.Conflict(
                    OrderErrorCodes.OrderAlreadyDelivered,
                    "Delivered orders cannot start delivery again."));
        }

        if (order.Status != OrderStatus.READY_FOR_DELIVERY)
        {
            return BadRequest<OrderDeliveryStartDto>(
                OrderErrorCodes.InvalidOrderStatus,
                "Order must be READY_FOR_DELIVERY before delivery can start.");
        }

        var productionError = await ValidateProductionCompletedForDeliveryAsync<OrderDeliveryStartDto>(
            order.OrderId,
            cancellationToken);
        if (productionError is not null)
        {
            return productionError;
        }

        if (!await _schedules.HasConfirmedDeliveryScheduleAsync(order.ProjectId, cancellationToken))
        {
            return BadRequest<OrderDeliveryStartDto>(
                OrderErrorCodes.DeliveryScheduleNotConfirmed,
                "At least one delivery schedule must be confirmed before delivery can start.");
        }

        if (!await _orders.AllDeliverableItemsReadyAsync(order.OrderId, cancellationToken))
        {
            return ServiceResult<OrderDeliveryStartDto>.Failure(
                Error.Conflict(
                    OrderErrorCodes.DeliverableItemsNotReady,
                    "All deliverable order items must be READY before delivery can start."));
        }

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.DELIVERING;
        order.UpdatedAt = now;
        project.Status = ProjectStatus.DELIVERING;
        project.UpdatedAt = now;

        _orders.Update(order);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        return ServiceResult<OrderDeliveryStartDto>.Success(
            ToDeliveryStartDto(order, project),
            "Delivery started successfully.");
    }

    public async Task<ServiceResult<OrderDeliveryCompletionDto>> CompleteDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDeliveryCompletionDto>.Unauthorized();
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderDeliveryCompletionDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<OrderDeliveryCompletionDto>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStartDelivery(role, project.AssignedSalesId, currentUserId))
        {
            return ServiceResult<OrderDeliveryCompletionDto>.Forbidden(ForbiddenMessage);
        }

        if (order.Status != OrderStatus.DELIVERING)
        {
            return BadRequest<OrderDeliveryCompletionDto>(
                OrderErrorCodes.OrderNotDelivering,
                "Order must be DELIVERING before delivery can be completed.");
        }

        var productionError = await ValidateProductionCompletedForDeliveryAsync<OrderDeliveryCompletionDto>(
            order.OrderId,
            cancellationToken);
        if (productionError is not null)
        {
            return productionError;
        }

        if (await _orders.AllDeliverableItemsDeliveredAsync(order.OrderId, cancellationToken))
        {
            return ServiceResult<OrderDeliveryCompletionDto>.Success(
                ToDeliveryCompletionDto(order, project, 0),
                "Delivery already completed for all deliverable items.");
        }

        if (!await _orders.AllDeliverableItemsReadyAsync(order.OrderId, cancellationToken))
        {
            return ServiceResult<OrderDeliveryCompletionDto>.Failure(
                Error.Conflict(
                    OrderErrorCodes.DeliverableItemsNotReady,
                    "All deliverable order items must be READY before delivery can be completed."));
        }

        var now = DateTime.UtcNow;
        var items = await _orders.GetItemsByOrderAsync(order.OrderId, cancellationToken);
        var deliveredCount = 0;
        foreach (var item in items.Where(IsActiveDeliveryItem))
        {
            item.Status = OrderItemStatus.DELIVERED;
            item.DeliveredAt = now;
            item.DeliveredBy = currentUserId;
            _orders.UpdateItem(item);
            deliveredCount++;
        }

        order.UpdatedAt = now;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await OrderNotificationSupport.TryDispatchUpdatedAsync(
            _notifications,
            _logger,
            order,
            project,
            cancellationToken);

        return ServiceResult<OrderDeliveryCompletionDto>.Success(
            ToDeliveryCompletionDto(order, project, deliveredCount),
            "Delivery completed successfully.");
    }

    public async Task<ServiceResult<OrderDeliveryConfirmationDto>> ConfirmDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OrderDeliveryConfirmationDto>.Unauthorized();
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<OrderDeliveryConfirmationDto>(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<OrderDeliveryConfirmationDto>(OrderErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var accessError = ValidateCustomerDeliveryAccess<OrderDeliveryConfirmationDto>(
            role,
            project.CustomerId,
            currentUserId);
        if (accessError is not null)
        {
            return accessError;
        }

        var productionError = await ValidateProductionCompletedForDeliveryAsync<OrderDeliveryConfirmationDto>(
            order.OrderId,
            cancellationToken);
        if (productionError is not null)
        {
            return productionError;
        }

        if (IsDeliveryAlreadyConfirmed(order))
        {
            return ServiceResult<OrderDeliveryConfirmationDto>.Success(
                ToOrderDeliveryConfirmationDto(order, project),
                "Order delivery confirmed successfully.");
        }

        if (order.Status != OrderStatus.DELIVERING)
        {
            return BadRequest<OrderDeliveryConfirmationDto>(
                OrderErrorCodes.OrderNotDelivering,
                "Order must be DELIVERING before delivery can be confirmed.");
        }

        if (!await _orders.AllDeliverableItemsDeliveredAsync(order.OrderId, cancellationToken))
        {
            return ServiceResult<OrderDeliveryConfirmationDto>.Failure(
                Error.Conflict(
                    OrderErrorCodes.DeliverableItemsNotDelivered,
                    "All deliverable order items must be delivered before confirmation."));
        }

        var now = DateTime.UtcNow;
        var remainingPaymentNotifications = new List<Payment>();
        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async transactionCancellationToken =>
            {
                order.CustomerConfirmedDeliveryAt = now;
                order.UpdatedAt = now;
                project.Status = ProjectStatus.DELIVERED;
                project.UpdatedAt = now;

                var remainingPayment = await ApplyPostDeliveryFinancialStateAsync(
                    order,
                    now,
                    transactionCancellationToken);
                if (remainingPayment is not null)
                {
                    remainingPaymentNotifications.Add(remainingPayment);
                }

                _orders.Update(order);
                _projects.Update(project);
                await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
            },
            cancellationToken);

        await OrderNotificationSupport.TryDispatchDeliveredAsync(
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
        foreach (var payment in remainingPaymentNotifications)
        {
            await PaymentNotificationSupport.TryDispatchCreatedAsync(
                _notifications,
                _logger,
                payment,
                cancellationToken);
        }

        return ServiceResult<OrderDeliveryConfirmationDto>.Success(
            ToOrderDeliveryConfirmationDto(order, project),
            "Order delivery confirmed successfully.");
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

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is not null)
        {
            await OrderNotificationSupport.TryDispatchUpdatedAsync(
                _notifications,
                _logger,
                order,
                project,
                cancellationToken);
        }

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
                "Order completed successfully.");
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
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await OrderNotificationSupport.TryDispatchCompletedAsync(
            _notifications,
            _logger,
            order,
            project,
            cancellationToken);

        return ServiceResult<OrderCompletionDto>.Success(
            ToCompletionDto(order, project, now),
            "Order completed successfully.");
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
        if (!OrderFinancialCompletionEvaluator.AreDeliverableItemsDelivered(items))
        {
            return BadRequest<T>(
                OrderErrorCodes.DeliveryNotCompleted,
                "Delivery must be fully confirmed before order completion.");
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

    private async Task<Payment?> ApplyPostDeliveryFinancialStateAsync(
        Order order,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var paidAmount = await _payments.SumOrderScopedPaidAmountAsync(order.OrderId, cancellationToken);
        var (recalculatedPaidAmount, remainingAmount) = OrderPaidAmountRecalculator.Calculate(
            order.FinalTotalAmount,
            paidAmount);

        order.PaidAmount = recalculatedPaidAmount;
        order.RemainingAmount = remainingAmount;

        if (remainingAmount <= 0m)
        {
            order.Status = OrderStatus.COMPLETED;
            return null;
        }

        var payment = await GetOrCreateRemainingPaymentAsync(order, remainingAmount, utcNow, cancellationToken);
        order.Status = OrderStatus.FINAL_PAYMENT_PENDING;
        return payment;
    }

    private async Task<Payment> GetOrCreateRemainingPaymentAsync(
        Order order,
        decimal remainingAmount,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await _payments.GetByOrderAndTypeAsync(
            order.OrderId,
            PaymentType.REMAINING_PAYMENT,
            cancellationToken);
        var reusable = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
            _payments,
            _unitOfWork,
            existing,
            cancellationToken);
        if (reusable is not null && ActivePaymentResolver.IsActive(reusable, utcNow))
        {
            return reusable;
        }

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            QuotationId = order.QuotationId,
            PaymentCode = await GenerateUniquePaymentCodeAsync(cancellationToken),
            PaidBy = order.CustomerId,
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = remainingAmount,
            Currency = _sePayOptions.Currency,
            Status = PaymentStatus.PENDING,
            ExpiredAt = utcNow.AddDays(RemainingPaymentExpiryDays),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _payments.AddPaymentAsync(payment, cancellationToken);
        return payment;
    }

    private async Task<string> GenerateUniquePaymentCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < PaymentServiceConstants.MaxPaymentCodeAttempts; attempt++)
        {
            var code = PaymentCodeGenerator.Generate(
                _sePayOptions.PaymentCodePrefix,
                _sePayOptions.PaymentCodeRandomDigits);
            if (!await _payments.PaymentCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique payment code.");
    }

    private async Task<ServiceResult<T>?> ValidateProductionCompletedForDeliveryAsync<T>(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await _productionRequests.IsOrderProductionCompletedAsync(orderId, cancellationToken)
            ? null
            : ServiceResult<T>.Failure(
                Error.Conflict(
                    OrderErrorCodes.ProductionNotCompleted,
                    "Production must be completed before delivery can proceed."));
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

    private static bool IsDeliveryAlreadyConfirmed(Order order)
    {
        return order.CustomerConfirmedDeliveryAt.HasValue &&
            order.Status is OrderStatus.DELIVERED or OrderStatus.FINAL_PAYMENT_PENDING or OrderStatus.COMPLETED;
    }

    private static ServiceResult<OrderListResponseDto> NotFoundList(string errorCode, string message)
    {
        return ServiceResult<OrderListResponseDto>.Failure(Error.NotFound(errorCode, message));
    }

    private static ServiceResult<OrderDetailDto> NotFoundDetail(string errorCode, string message)
    {
        return ServiceResult<OrderDetailDto>.Failure(Error.NotFound(errorCode, message));
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

    private static OrderDeliveryCompletionDto ToDeliveryCompletionDto(
        Order order,
        Project project,
        int deliveredItemCount)
    {
        return new OrderDeliveryCompletionDto
        {
            OrderId = order.OrderId,
            ProjectId = project.ProjectId,
            OrderStatus = order.Status.ToString() ?? string.Empty,
            DeliveredItemCount = deliveredItemCount,
            UpdatedAt = order.UpdatedAt
        };
    }

    private static OrderDeliveryConfirmationDto ToOrderDeliveryConfirmationDto(Order order, Project project)
    {
        return new OrderDeliveryConfirmationDto
        {
            OrderId = order.OrderId,
            ProjectId = project.ProjectId,
            OrderStatus = order.Status.ToString() ?? string.Empty,
            ProjectStatus = project.Status.ToString() ?? string.Empty,
            CustomerConfirmedDeliveryAt = order.CustomerConfirmedDeliveryAt
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
}
