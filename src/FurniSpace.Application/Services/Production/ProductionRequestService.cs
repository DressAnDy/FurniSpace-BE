#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Production;

public sealed class ProductionRequestService : IProductionRequestService
{
    private const string AdminRole = "ADMIN";
    private const string SalesRole = "SALES";
    private const string OrderReferenceType = "ORDER";
    private const string ProductionStaffNotFoundMessage = "Production staff not found.";
    private const string OrderNotFoundMessage = "Order not found.";
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string ProductionRequestNotFoundMessage = "Production request not found.";

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

        if (!await _productionRequests.IsActiveProductionStaffAsync(request.AssignedTo, cancellationToken))
        {
            return NotFound<ProductionRequestCreatedDto>(
                ProductionErrorCodes.ProductionStaffNotFound,
                ProductionStaffNotFoundMessage);
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
        var now = DateTime.UtcNow;
        var productionRequest = await BuildProductionRequestAsync(order, request, now, cancellationToken);
        var productionItems = BuildProductionItems(productOrderItems, productionRequest.ProductionRequestId, request);

        try
        {
            await _dependencies.UnitOfWork.BeginTransactionAsync(cancellationToken);
            await _productionRequests.AddAsync(productionRequest, cancellationToken);
            await _productionRequests.AddItemsAsync(productionItems, cancellationToken);
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

    private static void MoveOrderAndProjectToProduction(Order order, Project project, DateTime now)
    {
        order.Status = OrderStatus.IN_PRODUCTION;
        order.UpdatedAt = now;
        project.Status = ProjectStatus.IN_PRODUCTION;
        project.UpdatedAt = now;
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
                    ["ProjectName"] = project.ProjectName
                },
                [productionRequest.AssignedTo.Value],
                project.ProjectId,
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

    private static string NormalizePriority(string? priority)
    {
        return string.IsNullOrWhiteSpace(priority)
            ? "NORMAL"
            : priority.Trim().ToUpperInvariant();
    }

    private static ServiceResult<T> BadRequest<T>(string code, string message)
    {
        return ServiceResult<T>.Failure(Error.BadRequest(code, message));
    }

    private static ServiceResult<T> NotFound<T>(string code, string message)
    {
        return ServiceResult<T>.Failure(Error.NotFound(code, message));
    }
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
    public const string InvalidProductionRequestDate = "INVALID_PRODUCTION_REQUEST_DATE";
    public const string InvalidProductionStaffFilter = "INVALID_PRODUCTION_STAFF_FILTER";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string ProductionRequestAlreadyExists = "PRODUCTION_REQUEST_ALREADY_EXISTS";
    public const string ProductionRequestNotFound = "PRODUCTION_REQUEST_NOT_FOUND";
    public const string ProductionStaffNotFound = "PRODUCTION_STAFF_NOT_FOUND";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
}
