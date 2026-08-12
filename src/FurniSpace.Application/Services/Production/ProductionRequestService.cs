#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Production;

public sealed class ProductionRequestService : IProductionRequestService
{
    private const string AdminRole = "ADMIN";
    private const string ProductionRole = "PRODUCTION";
    private const string SalesRole = "SALES";
    private const string OrderReferenceType = "ORDER";
    private const string ProductionStaffNotFoundMessage = "Production staff not found.";
    private const string OrderNotFoundMessage = "Order not found.";
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string ProductionRequestNotFoundMessage = "Production request not found.";
    private const string ProductionItemNotFoundMessage = "Production item not found.";
    private static readonly ProductionRequestStatus[] AssignableStatuses =
    [
        ProductionRequestStatus.PENDING_REVIEW,
        ProductionRequestStatus.FEASIBLE,
        ProductionRequestStatus.IN_PRODUCTION
    ];

    private readonly IProductionRequestRepository _productionRequests;
    private readonly IOrderRepository _orders;
    private readonly IProjectRepository _projects;
    private readonly IPaymentRepository _payments;
    private readonly ProductionRequestServiceDependencies _dependencies;

    public ProductionRequestService(
        IProductionRequestRepository productionRequests,
        IOrderRepository orders,
        IProjectRepository projects,
        IPaymentRepository payments,
        ProductionRequestServiceDependencies dependencies)
    {
        _productionRequests = productionRequests;
        _orders = orders;
        _projects = projects;
        _payments = payments;
        _dependencies = dependencies;
    }

    public async Task<ServiceResult<ProductionRequestCreatedDto>> CreateAsync(
        Guid orderId,
        Guid currentUserId,
        CreateProductionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateSalesAdminAsync<ProductionRequestCreatedDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound<ProductionRequestCreatedDto>(
                ProductionErrorCodes.OrderNotFound,
                OrderNotFoundMessage);
        }

        if (order.Status != OrderStatus.DEPOSIT_PAID)
        {
            return BadRequest<ProductionRequestCreatedDto>(
                ProductionErrorCodes.InvalidOrderStatus,
                "Order status must be DEPOSIT_PAID.");
        }

        var depositPayment = await _payments.GetByOrderAndTypeAsync(
            orderId,
            PaymentType.DEPOSIT,
            cancellationToken);
        if (depositPayment?.Status != PaymentStatus.PAID)
        {
            return BadRequest<ProductionRequestCreatedDto>(
                ProductionErrorCodes.DepositNotPaid,
                "Deposit payment must be PAID.");
        }

        var assigneeError = await ValidateProductionAssigneeAsync<ProductionRequestCreatedDto>(
            request.AssignedTo,
            cancellationToken);
        if (assigneeError is not null)
        {
            return assigneeError;
        }

        if (await _productionRequests.HasActiveRequestForOrderAsync(orderId, cancellationToken))
        {
            return ServiceResult<ProductionRequestCreatedDto>.Failure(Error.Conflict(
                ProductionErrorCodes.ProductionRequestAlreadyExists,
                "Active production request already exists for this order."));
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<ProductionRequestCreatedDto>(
                ProductionErrorCodes.ProjectNotFound,
                ProjectNotFoundMessage);
        }

        var productOrderItems = await _productionRequests.GetProductOrderItemsAsync(orderId, cancellationToken);
        if (productOrderItems.Count == 0)
        {
            return BadRequest<ProductionRequestCreatedDto>(
                ProductionErrorCodes.OrderItemNotEligibleForProduction,
                "Order must contain at least one product item eligible for production.");
        }

        var orderItemTransitionError = ValidateOrderItemTransitions<ProductionRequestCreatedDto>(
            productOrderItems,
            OrderItemStatus.IN_PRODUCTION,
            OrderItemStatusTransitionOwner.ProductionRequestCreation);
        if (orderItemTransitionError is not null)
        {
            return orderItemTransitionError;
        }

        var now = DateTime.UtcNow;
        var productionRequest = await BuildProductionRequestAsync(order, request, now, cancellationToken);
        var productionItems = BuildProductionItems(productOrderItems, productionRequest.ProductionRequestId, request);

        try
        {
            await _dependencies.UnitOfWork.BeginTransactionAsync(cancellationToken);
            await _productionRequests.AddAsync(productionRequest, cancellationToken);
            await _productionRequests.AddItemsAsync(productionItems, cancellationToken);
            ApplyOrderItemStatusTransitions(
                productOrderItems,
                OrderItemStatus.IN_PRODUCTION,
                OrderItemStatusTransitionOwner.ProductionRequestCreation);
            MoveOrderAndProjectToProduction(order, project, now);
            _orders.Update(order);
            _projects.Update(project);
            await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _dependencies.UnitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _dependencies.UnitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await DispatchAssignedNotificationAsync(productionRequest, project, cancellationToken);

        var response = productionRequest.Adapt<ProductionRequestCreatedDto>();
        response.Status = productionRequest.Status.ToString() ?? string.Empty;
        response.ProductionItemCount = productionItems.Count;
        return ServiceResult<ProductionRequestCreatedDto>.Created(
            response,
            "Production request created successfully.");
    }

    public async Task<ServiceResult<List<AvailableProductionStaffDto>>> GetAvailableStaffAsync(
        Guid currentUserId,
        AvailableProductionStaffQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateSalesAdminAsync<List<AvailableProductionStaffDto>>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        if (query.Search?.Length > 150)
        {
            return BadRequest<List<AvailableProductionStaffDto>>(
                ProductionErrorCodes.InvalidProductionStaffFilter,
                "Search must not exceed 150 characters.");
        }

        if (query.ProjectId.HasValue &&
            await _projects.GetByIdAsync(query.ProjectId.Value, cancellationToken) is null)
        {
            return NotFound<List<AvailableProductionStaffDto>>(
                ProductionErrorCodes.ProjectNotFound,
                ProjectNotFoundMessage);
        }

        if (query.ProductionRequestId.HasValue &&
            await _productionRequests.GetByIdAsync(query.ProductionRequestId.Value, cancellationToken) is null)
        {
            return NotFound<List<AvailableProductionStaffDto>>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        var staff = await _productionRequests.GetAvailableStaffAsync(query.Search, cancellationToken);
        return ServiceResult<List<AvailableProductionStaffDto>>.Success(
            staff.Select(item => new AvailableProductionStaffDto
            {
                AccountId = item.AccountId,
                FullName = item.FullName,
                Email = item.Email,
                AvatarUrl = item.AvatarUrl,
                AccountStatus = item.AccountStatus.ToString() ?? string.Empty,
                ActiveRequestCount = item.ActiveRequestCount,
                PendingReviewRequestCount = item.PendingReviewRequestCount,
                InProductionRequestCount = item.InProductionRequestCount,
                BlockedRequestCount = item.BlockedRequestCount,
                IsAvailable = item.IsAvailable
            }).ToList(),
            "Available Production Staff retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionRequestAssignmentDto>> AssignAsync(
        Guid productionRequestId,
        Guid currentUserId,
        AssignProductionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateSalesAdminAsync<ProductionRequestAssignmentDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var assigneeError = await ValidateProductionAssigneeAsync<ProductionRequestAssignmentDto>(
            request.AssignedTo,
            cancellationToken);
        if (assigneeError is not null)
        {
            return assigneeError;
        }

        var detail = await _productionRequests.GetDetailAsync(productionRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFound<ProductionRequestAssignmentDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageProductionRequest(role, detail.AssignedSalesId, currentUserId))
        {
            return ServiceResult<ProductionRequestAssignmentDto>.Forbidden(
                "You do not have permission to assign this production request.");
        }

        if (!detail.Status.HasValue || !AssignableStatuses.Contains(detail.Status.Value))
        {
            return BadRequest<ProductionRequestAssignmentDto>(
                ProductionErrorCodes.ProductionRequestAlreadyClosed,
                "Production request is already closed.");
        }

        var productionRequest = await _productionRequests.GetByIdAsync(productionRequestId, cancellationToken);
        if (productionRequest is null)
        {
            return NotFound<ProductionRequestAssignmentDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        var previousAssignedTo = productionRequest.AssignedTo;
        productionRequest.AssignedTo = request.AssignedTo;
        productionRequest.Note = MergeAssignmentNote(productionRequest.Note, request.AssignmentNote);
        productionRequest.UpdatedAt = DateTime.UtcNow;
        _productionRequests.Update(productionRequest);
        await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);

        await DispatchAssignedNotificationAsync(productionRequest, detail.ProjectName, cancellationToken);

        return ServiceResult<ProductionRequestAssignmentDto>.Success(
            new ProductionRequestAssignmentDto
            {
                ProductionRequestId = productionRequest.ProductionRequestId,
                PreviousAssignedTo = previousAssignedTo,
                AssignedTo = productionRequest.AssignedTo,
                Status = productionRequest.Status.ToString() ?? string.Empty,
                UpdatedAt = productionRequest.UpdatedAt
            },
            "Production request assigned successfully.");
    }

    public async Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
        Guid currentUserId,
        ProductionRequestQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionRequestListResponseDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProductionQueue(role))
        {
            return ServiceResult<ProductionRequestListResponseDto>.Forbidden(
                "You do not have permission to view production requests.");
        }

        var items = await _productionRequests.GetQueueAsync(
            new ProductionRequestQueueReadModel
            {
                Status = query.Status,
                AssignedTo = query.AssignedTo,
                Priority = NormalizeOptionalPriority(query.Priority),
                CurrentUserRole = role,
                CurrentUserId = currentUserId
            },
            cancellationToken);

        return ServiceResult<ProductionRequestListResponseDto>.Success(
            new ProductionRequestListResponseDto
            {
                Items = items.Select(ToListItemDto).ToList()
            },
            "Production requests retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
        Guid productionRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionRequestDetailDto>.Unauthorized();
        }

        var detail = await _productionRequests.GetDetailAsync(productionRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFound<ProductionRequestDetailDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProductionRequest(role, detail.AssignedSalesId, currentUserId))
        {
            return ServiceResult<ProductionRequestDetailDto>.Forbidden(
                "You do not have permission to view this production request.");
        }

        return ServiceResult<ProductionRequestDetailDto>.Success(
            ToDetailDto(detail),
            "Production request detail retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionRequestStatusDto>> MarkFeasibleAsync(
        Guid productionRequestId,
        Guid currentUserId,
        MarkProductionRequestFeasibleDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateProductionAdminAsync<ProductionRequestStatusDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var productionRequest = await _productionRequests.GetByIdAsync(productionRequestId, cancellationToken);
        if (productionRequest is null)
        {
            return NotFound<ProductionRequestStatusDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        if (productionRequest.Status != ProductionRequestStatus.PENDING_REVIEW)
        {
            return InvalidRequestTransition<ProductionRequestStatusDto>();
        }

        var now = DateTime.UtcNow;
        productionRequest.Status = ProductionRequestStatus.FEASIBLE;
        productionRequest.Note = MergeNote(productionRequest.Note, request.Note);
        productionRequest.UpdatedAt = now;
        _productionRequests.Update(productionRequest);
        await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductionRequestStatusDto>.Success(
            ToStatusDto(productionRequest),
            "Production request marked feasible successfully.");
    }

    public async Task<ServiceResult<ProductionRequestStatusDto>> StartAsync(
        Guid productionRequestId,
        Guid currentUserId,
        StartProductionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateProductionAdminAsync<ProductionRequestStatusDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var productionRequest = await _productionRequests.GetByIdAsync(productionRequestId, cancellationToken);
        if (productionRequest is null)
        {
            return NotFound<ProductionRequestStatusDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        if (productionRequest.Status is not ProductionRequestStatus.FEASIBLE)
        {
            return InvalidRequestTransition<ProductionRequestStatusDto>();
        }

        var now = DateTime.UtcNow;
        productionRequest.Status = ProductionRequestStatus.IN_PRODUCTION;
        productionRequest.ActualStartDate = DateOnly.FromDateTime(now);
        productionRequest.UpdatedAt = now;
        _productionRequests.Update(productionRequest);
        await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductionRequestStatusDto>.Success(
            ToStatusDto(productionRequest),
            "Production request started successfully.");
    }

    public async Task<ServiceResult<ProductionItemStatusDto>> UpdateItemStatusAsync(
        Guid productionItemId,
        Guid currentUserId,
        UpdateProductionItemStatusDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateProductionAdminAsync<ProductionItemStatusDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        if (!request.Status.HasValue)
        {
            return InvalidItemTransition();
        }

        var item = await _productionRequests.GetItemByIdAsync(productionItemId, cancellationToken);
        if (item is null)
        {
            return NotFound<ProductionItemStatusDto>(
                ProductionErrorCodes.ProductionItemNotFound,
                ProductionItemNotFoundMessage);
        }

        if (!CanMoveProductionItem(item.Status, request.Status.Value))
        {
            return InvalidItemTransition();
        }

        var cancellationReason = request.CancellationReason?.Trim();
        if (request.Status == ProductionItemStatus.CANCELLED && string.IsNullOrWhiteSpace(cancellationReason))
        {
            return BadRequest<ProductionItemStatusDto>(
                ProductionErrorCodes.ProductionCancellationReasonRequired,
                "Cancellation reason is required.");
        }

        var detail = await _productionRequests.GetDetailByItemIdAsync(productionItemId, cancellationToken);
        var now = DateTime.UtcNow;
        ApplyItemStatus(item, request, cancellationReason, now);
        _productionRequests.UpdateItem(item);
        await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);

        if (request.Status == ProductionItemStatus.CANCELLED && detail is not null)
        {
            await DispatchItemCancelledNotificationAsync(item, detail, cancellationToken);
        }

        return ServiceResult<ProductionItemStatusDto>.Success(
            ToItemStatusDto(item),
            "Production item status updated successfully.");
    }

    public async Task<ServiceResult<ProductionCompletionDto>> CompleteAsync(
        Guid productionRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateProductionAdminAsync<ProductionCompletionDto>(
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var productionRequest = await _productionRequests.GetByIdAsync(productionRequestId, cancellationToken);
        if (productionRequest is null)
        {
            return NotFound<ProductionCompletionDto>(
                ProductionErrorCodes.ProductionRequestNotFound,
                ProductionRequestNotFoundMessage);
        }

        var order = await _orders.GetByIdAsync(productionRequest.OrderId, cancellationToken);
        if (order is null)
        {
            return NotFound<ProductionCompletionDto>(ProductionErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(productionRequest.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound<ProductionCompletionDto>(ProductionErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        if (productionRequest.Status == ProductionRequestStatus.COMPLETED)
        {
            var completedCounts = await CountSynchronizedOrderItemsAsync(order.OrderId, cancellationToken);
            return ServiceResult<ProductionCompletionDto>.Success(
                ToCompletionDto(
                    productionRequest,
                    order,
                    project,
                    completedCounts),
                "Production completed successfully.");
        }

        if (productionRequest.Status != ProductionRequestStatus.IN_PRODUCTION)
        {
            return InvalidRequestTransition<ProductionCompletionDto>();
        }

        var productionItems = await _productionRequests.GetItemsByRequestIdAsync(
            productionRequestId,
            cancellationToken);
        if (productionItems.Any(item => !IsResolvedProductionItem(item)))
        {
            return BadRequest<ProductionCompletionDto>(
                ProductionErrorCodes.ProductionItemsNotResolved,
                "All production items must be completed or cancelled before completing production.");
        }

        var orderItemSyncError = await ValidateResolvedOrderItemTransitionsAsync<ProductionCompletionDto>(
            productionItems,
            cancellationToken);
        if (orderItemSyncError is not null)
        {
            return orderItemSyncError;
        }

        var now = DateTime.UtcNow;
        try
        {
            await _dependencies.UnitOfWork.BeginTransactionAsync(cancellationToken);
            await SyncResolvedOrderItemsAsync(
                productionItems,
                currentUserId,
                now,
                cancellationToken);
            CompleteWorkflow(productionRequest, order, project, now);
            await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _dependencies.UnitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _dependencies.UnitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var synchronizedCounts = await CountSynchronizedOrderItemsAsync(order.OrderId, cancellationToken);
        return ServiceResult<ProductionCompletionDto>.Success(
            ToCompletionDto(
                productionRequest,
                order,
                project,
                synchronizedCounts),
            "Production completed successfully.");
    }

    private async Task<ProductionRequest> BuildProductionRequestAsync(
        Order order,
        CreateProductionRequestDto request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sequence = await _productionRequests.CountCreatedOnAsync(DateOnly.FromDateTime(now), cancellationToken) + 1;
        return new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProductionCode = $"PRD-{now:yyyyMMdd}-{sequence:000000}",
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            AssignedTo = request.AssignedTo,
            Status = ProductionRequestStatus.PENDING_REVIEW,
            Priority = NormalizePriority(request.Priority),
            EstimatedStartDate = request.EstimatedStartDate,
            EstimatedCompletionDate = request.EstimatedCompletionDate,
            Note = request.Note?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static List<ProductionItem> BuildProductionItems(
        List<OrderItem> orderItems,
        Guid productionRequestId,
        CreateProductionRequestDto request)
    {
        return orderItems
            .DistinctBy(item => item.OrderItemId)
            .Select(item => new ProductionItem
            {
                ProductionItemId = Guid.NewGuid(),
                ProductionRequestId = productionRequestId,
                OrderItemId = item.OrderItemId,
                ProductVersionId = item.ProductVersionId,
                ProductNameSnapshot = item.ProductNameSnapshot,
                ProductVersionNameSnapshot = item.ProductVersionNameSnapshot,
                Quantity = item.Quantity,
                Status = ProductionItemStatus.PENDING,
                EstimatedCompletionDate = request.EstimatedCompletionDate,
                ProductionNote = item.ProductionNote,
                StartedAt = null,
                CompletedAt = null
            })
            .ToList();
    }

    private static ServiceResult<T>? ValidateOrderItemTransitions<T>(
        IEnumerable<OrderItem> orderItems,
        OrderItemStatus targetStatus,
        OrderItemStatusTransitionOwner owner)
    {
        foreach (var orderItem in orderItems)
        {
            var error = OrderItemStatusTransitionService.Validate(orderItem.Status, targetStatus, owner);
            if (error is not null)
            {
                return BadRequest<T>(error.ErrorCode, error.Message);
            }
        }

        return null;
    }

    private void ApplyOrderItemStatusTransitions(
        IEnumerable<OrderItem> orderItems,
        OrderItemStatus targetStatus,
        OrderItemStatusTransitionOwner owner)
    {
        foreach (var orderItem in orderItems)
        {
            var error = OrderItemStatusTransitionService.Validate(orderItem.Status, targetStatus, owner);
            if (error is not null)
            {
                throw new InvalidOperationException(error.Message);
            }

            orderItem.Status = targetStatus;
            _orders.UpdateItem(orderItem);
        }
    }

    private static void MoveOrderAndProjectToProduction(Order order, Project project, DateTime now)
    {
        order.Status = OrderStatus.IN_PRODUCTION;
        order.UpdatedAt = now;
        project.Status = ProjectStatus.IN_PRODUCTION;
        project.UpdatedAt = now;
    }

    private static bool IsResolvedProductionItem(ProductionItem item)
    {
        return item.Status is ProductionItemStatus.COMPLETED or ProductionItemStatus.CANCELLED;
    }

    private async Task<ServiceResult<T>?> ValidateResolvedOrderItemTransitionsAsync<T>(
        List<ProductionItem> productionItems,
        CancellationToken cancellationToken)
    {
        foreach (var productionItem in productionItems)
        {
            var orderItem = await _orders.GetItemByIdAsync(productionItem.OrderItemId, cancellationToken);
            if (orderItem is null)
            {
                return BadRequest<T>(
                    ProductionErrorCodes.OrderItemMappingInvalid,
                    "Production item is not mapped to a valid order item.");
            }

            var targetStatus = ResolveCompletedOrderItemStatus(productionItem);
            if (orderItem.Status == targetStatus)
            {
                continue;
            }

            var error = OrderItemStatusTransitionService.Validate(
                orderItem.Status,
                targetStatus,
                OrderItemStatusTransitionOwner.ProductionRequestCompletion);
            if (error is not null)
            {
                return BadRequest<T>(error.ErrorCode, error.Message);
            }
        }

        return null;
    }

    private async Task SyncResolvedOrderItemsAsync(
        List<ProductionItem> productionItems,
        Guid currentUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var productionItem in productionItems)
        {
            var orderItem = await _orders.GetItemByIdAsync(productionItem.OrderItemId, cancellationToken);
            if (orderItem is null)
            {
                throw new InvalidOperationException("Production item is not mapped to a valid order item.");
            }

            var targetStatus = ResolveCompletedOrderItemStatus(productionItem);
            if (orderItem.Status == targetStatus)
            {
                continue;
            }

            var error = OrderItemStatusTransitionService.Validate(
                orderItem.Status,
                targetStatus,
                OrderItemStatusTransitionOwner.ProductionRequestCompletion);
            if (error is not null)
            {
                throw new InvalidOperationException(error.Message);
            }

            orderItem.Status = targetStatus;
            if (targetStatus == OrderItemStatus.UNAVAILABLE)
            {
                ApplyUnavailableItemConfirmation(
                    orderItem,
                    productionItem,
                    currentUserId,
                    now);
            }

            _orders.UpdateItem(orderItem);
        }
    }

    private static OrderItemStatus ResolveCompletedOrderItemStatus(ProductionItem productionItem)
    {
        return productionItem.Status == ProductionItemStatus.CANCELLED
            ? OrderItemStatus.UNAVAILABLE
            : OrderItemStatus.READY;
    }

    private static void ApplyUnavailableItemConfirmation(
        OrderItem orderItem,
        ProductionItem productionItem,
        Guid currentUserId,
        DateTime now)
    {
        orderItem.AdjustmentAmount = orderItem.SubtotalAmount;
        orderItem.UnavailableReason = productionItem.CancellationReason;
        orderItem.UnavailableConfirmedBy = currentUserId;
        orderItem.UnavailableConfirmedAt = now;
    }

    private async Task<OrderItemSynchronizationCounts> CountSynchronizedOrderItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var orderItems = await _orders.GetItemsByOrderAsync(orderId, cancellationToken);
        return new OrderItemSynchronizationCounts(
            orderItems.Count(IsReadyProductItem),
            orderItems.Count(IsUnavailableProductItem));
    }

    private static bool IsReadyProductItem(OrderItem item)
    {
        return item.ProductVersionId.HasValue &&
            item.Status == OrderItemStatus.READY;
    }

    private static bool IsUnavailableProductItem(OrderItem item)
    {
        return item.ProductVersionId.HasValue &&
            item.Status == OrderItemStatus.UNAVAILABLE;
    }

    private void CompleteWorkflow(
        ProductionRequest productionRequest,
        Order order,
        Project project,
        DateTime now)
    {
        var paidAmount = order.PaidAmount ?? 0m;
        order.RemainingAmount = order.FinalTotalAmount - paidAmount;
        order.Status = OrderStatus.READY_FOR_DELIVERY;
        order.UpdatedAt = now;
        productionRequest.Status = ProductionRequestStatus.COMPLETED;
        productionRequest.ActualCompletionDate = DateOnly.FromDateTime(now);
        productionRequest.UpdatedAt = now;
        project.Status = ProjectStatus.READY_FOR_DELIVERY;
        project.UpdatedAt = now;

        _orders.Update(order);
        _productionRequests.Update(productionRequest);
        _projects.Update(project);
    }

    private async Task<ServiceResult<T>?> ValidateSalesAdminAsync<T>(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<T>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (SalesRole or AdminRole))
        {
            return ServiceResult<T>.Forbidden("You do not have permission to manage production requests.");
        }

        return null;
    }

    private async Task<ServiceResult<T>?> ValidateProductionAdminAsync<T>(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<T>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ProductionRole or AdminRole))
        {
            return ServiceResult<T>.Forbidden("You do not have permission to update production status.");
        }

        return null;
    }

    private async Task<ServiceResult<T>?> ValidateProductionAssigneeAsync<T>(
        Guid assignedTo,
        CancellationToken cancellationToken)
    {
        if (assignedTo == Guid.Empty)
        {
            return BadRequest<T>(
                ProductionErrorCodes.InvalidProductionAssignee,
                "Assigned production staff is required.");
        }

        var assignee = await _productionRequests.GetAssigneeAsync(assignedTo, cancellationToken);
        if (assignee is null)
        {
            return NotFound<T>(
                ProductionErrorCodes.ProductionStaffNotFound,
                ProductionStaffNotFoundMessage);
        }

        if (assignee.RoleName != ProductionRole)
        {
            return BadRequest<T>(
                ProductionErrorCodes.InvalidProductionAssignee,
                "Selected account does not have Production role.");
        }

        if (assignee.Status != AccountStatus.ACTIVE || assignee.DeletedAt is not null)
        {
            return BadRequest<T>(
                ProductionErrorCodes.ProductionAssigneeNotActive,
                "Selected Production account is not active.");
        }

        return null;
    }

    private static ServiceResult<ProductionRequestCreatedDto>? ValidateCreateRequest(
        CreateProductionRequestDto request)
    {
        if (request.AssignedTo == Guid.Empty)
        {
            return BadRequest<ProductionRequestCreatedDto>(
                ProductionErrorCodes.ProductionStaffNotFound,
                "Assigned production staff is required.");
        }

        if (request.EstimatedStartDate.HasValue &&
            request.EstimatedCompletionDate.HasValue &&
            request.EstimatedStartDate.Value > request.EstimatedCompletionDate.Value)
        {
            return BadRequest<ProductionRequestCreatedDto>(
                ProductionErrorCodes.InvalidProductionRequestDate,
                "Estimated start date must be before or equal to estimated completion date.");
        }

        return null;
    }

    private async Task DispatchAssignedNotificationAsync(
        ProductionRequest productionRequest,
        Project project,
        CancellationToken cancellationToken)
    {
        await DispatchAssignedNotificationAsync(productionRequest, project.ProjectName, cancellationToken);
    }

    private async Task DispatchAssignedNotificationAsync(
        ProductionRequest productionRequest,
        string projectName,
        CancellationToken cancellationToken)
    {
        if (_dependencies.Notifications is null || productionRequest.AssignedTo is null)
        {
            return;
        }

        try
        {
            await _dependencies.Notifications.DispatchAsync(
                NotificationType.ProductionRequestAssigned,
                new Dictionary<string, string>
                {
                    ["ProductionCode"] = productionRequest.ProductionCode ?? string.Empty,
                    ["ProjectName"] = projectName
                },
                [productionRequest.AssignedTo.Value],
                productionRequest.ProjectId,
                OrderReferenceType,
                productionRequest.OrderId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _dependencies.Logger?.LogWarning(
                exception,
                "Failed to dispatch production request assigned notification for request {ProductionRequestId}",
                productionRequest.ProductionRequestId);
        }
    }

    private async Task DispatchItemCancelledNotificationAsync(
        ProductionItem item,
        ProductionRequestDetailReadModel detail,
        CancellationToken cancellationToken)
    {
        if (_dependencies.Notifications is null || detail.AssignedSalesId is null)
        {
            return;
        }

        try
        {
            await _dependencies.Notifications.DispatchAsync(
                NotificationType.ProductionItemCancelled,
                new Dictionary<string, string>
                {
                    ["ProductName"] = item.ProductNameSnapshot ?? string.Empty,
                    ["ProductionCode"] = detail.ProductionCode ?? string.Empty
                },
                [detail.AssignedSalesId.Value],
                detail.ProjectId,
                OrderReferenceType,
                detail.OrderId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _dependencies.Logger?.LogWarning(
                exception,
                "Failed to dispatch production item cancelled notification for item {ProductionItemId}",
                item.ProductionItemId);
        }
    }

    private static string NormalizePriority(string? priority)
    {
        return string.IsNullOrWhiteSpace(priority)
            ? "NORMAL"
            : priority.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptionalPriority(string? priority)
    {
        return string.IsNullOrWhiteSpace(priority)
            ? null
            : priority.Trim().ToUpperInvariant();
    }

    private static bool CanManageProductionRequest(
        string? role,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return role == AdminRole || role == SalesRole && assignedSalesId == currentUserId;
    }

    private static bool CanViewProductionQueue(string? role)
    {
        return role is AdminRole or SalesRole or ProductionRole;
    }

    private static bool CanViewProductionRequest(
        string? role,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return role is AdminRole or ProductionRole ||
            role == SalesRole && assignedSalesId == currentUserId;
    }

    private static string? MergeAssignmentNote(string? currentNote, string? assignmentNote)
    {
        return MergeNote(currentNote, assignmentNote);
    }

    private static string? MergeNote(string? currentNote, string? newNote)
    {
        var note = newNote?.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return currentNote;
        }

        return string.IsNullOrWhiteSpace(currentNote)
            ? note
            : $"{currentNote.Trim()}{Environment.NewLine}{note}";
    }

    private static bool CanMoveProductionItem(
        ProductionItemStatus? currentStatus,
        ProductionItemStatus targetStatus)
    {
        return currentStatus switch
        {
            ProductionItemStatus.PENDING => targetStatus is
                ProductionItemStatus.IN_PRODUCTION or
                ProductionItemStatus.CANCELLED,
            ProductionItemStatus.IN_PRODUCTION => targetStatus is
                ProductionItemStatus.COMPLETED or
                ProductionItemStatus.CANCELLED,
            _ => false
        };
    }

    private static void ApplyItemStatus(
        ProductionItem item,
        UpdateProductionItemStatusDto request,
        string? cancellationReason,
        DateTime now)
    {
        var targetStatus = request.Status!.Value;
        item.Status = targetStatus;
        item.ProductionNote = MergeNote(item.ProductionNote, request.ProductionNote);

        if (targetStatus == ProductionItemStatus.IN_PRODUCTION && item.StartedAt is null)
        {
            item.StartedAt = now;
        }

        if (targetStatus == ProductionItemStatus.COMPLETED)
        {
            item.CompletedAt = now;
        }

        if (targetStatus == ProductionItemStatus.CANCELLED)
        {
            item.CancellationReason = cancellationReason;
        }
    }

    private static ProductionRequestListItemDto ToListItemDto(
        ProductionRequestListItemReadModel item)
    {
        return new ProductionRequestListItemDto
        {
            ProductionRequestId = item.ProductionRequestId,
            ProductionCode = item.ProductionCode,
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            AssignedTo = item.AssignedTo,
            AssignedToName = item.AssignedToName,
            Status = item.Status.ToString() ?? string.Empty,
            Priority = item.Priority,
            EstimatedStartDate = item.EstimatedStartDate,
            EstimatedCompletionDate = item.EstimatedCompletionDate,
            ProductionItemCount = item.ProductionItemCount,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static ProductionRequestDetailDto ToDetailDto(
        ProductionRequestDetailReadModel detail)
    {
        return new ProductionRequestDetailDto
        {
            ProductionRequestId = detail.ProductionRequestId,
            ProductionCode = detail.ProductionCode,
            ProjectId = detail.ProjectId,
            ProjectCode = detail.ProjectCode,
            ProjectName = detail.ProjectName,
            OrderId = detail.OrderId,
            OrderCode = detail.OrderCode,
            AssignedTo = detail.AssignedTo,
            AssignedToName = detail.AssignedToName,
            Status = detail.Status.ToString() ?? string.Empty,
            Priority = detail.Priority,
            EstimatedStartDate = detail.EstimatedStartDate,
            EstimatedCompletionDate = detail.EstimatedCompletionDate,
            ActualStartDate = detail.ActualStartDate,
            ActualCompletionDate = detail.ActualCompletionDate,
            CancellationReason = detail.CancellationReason,
            Note = detail.Note,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt,
            Items = detail.Items.Select(ToItemDto).ToList()
        };
    }

    private static ProductionRequestStatusDto ToStatusDto(ProductionRequest productionRequest)
    {
        return new ProductionRequestStatusDto
        {
            ProductionRequestId = productionRequest.ProductionRequestId,
            Status = productionRequest.Status.ToString() ?? string.Empty,
            ActualStartDate = productionRequest.ActualStartDate,
            UpdatedAt = productionRequest.UpdatedAt
        };
    }

    private static ProductionItemStatusDto ToItemStatusDto(ProductionItem item)
    {
        return new ProductionItemStatusDto
        {
            ProductionItemId = item.ProductionItemId,
            ProductionRequestId = item.ProductionRequestId,
            OrderItemId = item.OrderItemId,
            Status = item.Status.ToString() ?? string.Empty,
            ProductionNote = item.ProductionNote,
            CancellationReason = item.CancellationReason,
            StartedAt = item.StartedAt,
            CompletedAt = item.CompletedAt
        };
    }

    private static ProductionItemDto ToItemDto(ProductionItemReadModel item)
    {
        return new ProductionItemDto
        {
            ProductionItemId = item.ProductionItemId,
            ProductionRequestId = item.ProductionRequestId,
            OrderItemId = item.OrderItemId,
            ProductVersionId = item.ProductVersionId,
            ProductNameSnapshot = item.ProductNameSnapshot,
            ProductVersionNameSnapshot = item.ProductVersionNameSnapshot,
            Quantity = item.Quantity,
            Status = item.Status.ToString() ?? string.Empty,
            MaterialNote = item.MaterialNote,
            ProductionNote = item.ProductionNote,
            EstimatedCompletionDate = item.EstimatedCompletionDate,
            StartedAt = item.StartedAt,
            CompletedAt = item.CompletedAt,
            OrderItemStatus = item.OrderItemStatus.ToString()
        };
    }

    private static ServiceResult<T> BadRequest<T>(string code, string message)
    {
        return ServiceResult<T>.Failure(Error.BadRequest(code, message));
    }

    private static ServiceResult<T> NotFound<T>(string code, string message)
    {
        return ServiceResult<T>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<T> InvalidRequestTransition<T>()
    {
        return BadRequest<T>(
            ProductionErrorCodes.InvalidProductionRequestTransition,
            "Production request status transition is invalid.");
    }

    private static ServiceResult<ProductionItemStatusDto> InvalidItemTransition()
    {
        return BadRequest<ProductionItemStatusDto>(
            ProductionErrorCodes.InvalidProductionItemTransition,
            "Production item status transition is invalid.");
    }

    private static ProductionCompletionDto ToCompletionDto(
        ProductionRequest productionRequest,
        Order order,
        Project project,
        OrderItemSynchronizationCounts counts)
    {
        return new ProductionCompletionDto
        {
            ProductionRequestId = productionRequest.ProductionRequestId,
            ProductionStatus = productionRequest.Status.ToString() ?? string.Empty,
            OrderStatus = order.Status.ToString() ?? string.Empty,
            ProjectStatus = project.Status.ToString() ?? string.Empty,
            ActualStartDate = productionRequest.ActualStartDate,
            ActualCompletionDate = productionRequest.ActualCompletionDate,
            ReadyOrderItemCount = counts.ReadyOrderItemCount,
            UnavailableOrderItemCount = counts.UnavailableOrderItemCount,
            FinalTotalAmount = order.FinalTotalAmount,
            PaidAmount = order.PaidAmount,
            RemainingAmount = order.RemainingAmount
        };
    }

    private sealed record OrderItemSynchronizationCounts(
        int ReadyOrderItemCount,
        int UnavailableOrderItemCount);
}

public sealed class ProductionRequestServiceDependencies
{
    public ProductionRequestServiceDependencies(
        IUnitOfWork unitOfWork,
        INotificationDispatcher? notifications,
        ILogger<ProductionRequestService>? logger)
    {
        UnitOfWork = unitOfWork;
        Notifications = notifications;
        Logger = logger;
    }

    public IUnitOfWork UnitOfWork { get; }
    public INotificationDispatcher? Notifications { get; }
    public ILogger<ProductionRequestService>? Logger { get; }
}

public static class ProductionErrorCodes
{
    public const string DepositNotPaid = "DEPOSIT_NOT_PAID";
    public const string InvalidOrderStatus = "INVALID_ORDER_STATUS";
    public const string InvalidProductionAssignee = "INVALID_PRODUCTION_ASSIGNEE";
    public const string InvalidProductionItemTransition = "INVALID_PRODUCTION_ITEM_TRANSITION";
    public const string InvalidProductionRequestDate = "INVALID_PRODUCTION_REQUEST_DATE";
    public const string InvalidProductionRequestTransition = "INVALID_PRODUCTION_REQUEST_TRANSITION";
    public const string InvalidProductionStaffFilter = "INVALID_PRODUCTION_STAFF_FILTER";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderItemNotEligibleForProduction = "ORDER_ITEM_NOT_ELIGIBLE_FOR_PRODUCTION";
    public const string OrderItemMappingInvalid = "ORDER_ITEM_MAPPING_INVALID";
    public const string ProductionAssigneeNotActive = "PRODUCTION_ASSIGNEE_NOT_ACTIVE";
    public const string ProductionItemsNotResolved = "PRODUCTION_ITEMS_NOT_RESOLVED";
    public const string ProductionRequestAlreadyClosed = "PRODUCTION_REQUEST_ALREADY_CLOSED";
    public const string ProductionRequestAlreadyExists = "PRODUCTION_REQUEST_ALREADY_EXISTS";
    public const string ProductionRequestNotFound = "PRODUCTION_REQUEST_NOT_FOUND";
    public const string ProductionItemNotFound = "PRODUCTION_ITEM_NOT_FOUND";
    public const string ProductionCancellationReasonRequired = "PRODUCTION_CANCELLATION_REASON_REQUIRED";
    public const string ProductionStaffNotFound = "PRODUCTION_STAFF_NOT_FOUND";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
}
