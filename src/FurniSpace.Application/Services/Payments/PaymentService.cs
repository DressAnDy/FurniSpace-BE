using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Identity;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Payments;
using static FurniSpace.Application.Constants.Payments.PaymentServiceConstants;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace FurniSpace.Application.Services.Payments;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _payments;
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SePayOptions _sePayOptions;
    private readonly PayOsOptions _payOsOptions;
    private readonly SePayVietQrUrlBuilder _vietQrUrlBuilder;
    private readonly IPayOsClient _payOsClient;
    private readonly ProjectWorkflowSettings _projectWorkflowSettings;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<PaymentService>? _logger;

    public PaymentService(
        IPaymentRepository payments,
        IProjectRepository projects,
        IOrderRepository orders,
        PaymentServiceDependencies dependencies,
        INotificationDispatcher? notifications = null,
        ILogger<PaymentService>? logger = null)
    {
        _payments = payments;
        _projects = projects;
        _orders = orders;
        _unitOfWork = dependencies.UnitOfWork;
        _sePayOptions = dependencies.SePayOptions;
        _payOsOptions = dependencies.PayOsOptions;
        _projectWorkflowSettings = dependencies.ProjectWorkflowSettings;
        _vietQrUrlBuilder = dependencies.VietQrUrlBuilder;
        _payOsClient = dependencies.PayOsClient;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
        Guid currentUserId,
        CreateTestPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentDetailDto>.Unauthorized();
        }

        if (request.ProjectId == Guid.Empty)
        {
            return BadRequestDetail(PaymentErrorCodes.ProjectNotFound, "Project id is required.");
        }

        if (request.Amount <= 0m)
        {
            return BadRequestDetail(PaymentErrorCodes.InvalidPaymentAmount, "Amount must be greater than zero.");
        }

        var project = await _projects.GetDetailAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(PaymentErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var paymentCode = await GenerateUniquePaymentCodeAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            PaymentCode = paymentCode,
            PaidBy = project.CustomerId,
            PaymentType = request.PaymentType,
            Amount = request.Amount,
            Currency = _sePayOptions.Currency,
            Status = PaymentStatus.PENDING,
            ExpiredAt = request.ExpiredAt,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _payments.AddPaymentAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detail = await _payments.GetDetailAsync(payment.PaymentId, cancellationToken);
        return ServiceResult<PaymentDetailDto>.Created(
            detail?.Adapt<PaymentDetailDto>() ?? payment.Adapt<PaymentDetailDto>(),
            "Test payment created successfully.");
    }

    public async Task<ServiceResult<PaymentDetailDto>> CreateDepositPaymentForOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderDepositPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentDetailDto>.Unauthorized();
        }

        var order = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFoundDetail(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!OrderAccessEvaluator.CanManageDepositPayment(
                role,
                order.CustomerId,
                order.AssignedSalesId,
                currentUserId))
        {
            return ServiceResult<PaymentDetailDto>.Forbidden("You do not have permission to create this deposit payment.");
        }

        if (order.Status == OrderStatus.DEPOSIT_PENDING)
        {
            var existing = await _payments.GetByOrderAndTypeAsync(orderId, PaymentType.DEPOSIT, cancellationToken);
            if (existing?.Status == PaymentStatus.PAID)
            {
                return BadRequestDetail(OrderErrorCodes.DepositAlreadyPaid, "Deposit payment has already been paid.");
            }

            var reusable = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
                _payments,
                _unitOfWork,
                existing,
                cancellationToken);
            if (reusable is not null && ActivePaymentResolver.IsActive(reusable, DateTime.UtcNow))
            {
                var existingDetail = await _payments.GetDetailAsync(reusable.PaymentId, cancellationToken);
                return ServiceResult<PaymentDetailDto>.Success(
                    PaymentServiceActivePaymentSupport.ToDetailDto(existingDetail, reusable, reused: true),
                    "Active payment retrieved successfully.");
            }
        }
        else if (order.Status != OrderStatus.CREATED)
        {
            return BadRequestDetail(OrderErrorCodes.InvalidOrderStatus, "Order is not ready for deposit payment.");
        }

        var depositAmount = order.DepositAmount ?? 0m;
        if (depositAmount <= 0m)
        {
            return BadRequestDetail(PaymentErrorCodes.InvalidPaymentAmount, "Deposit amount must be greater than zero.");
        }

        var activeDeposit = await _payments.GetByOrderAndTypeAsync(orderId, PaymentType.DEPOSIT, cancellationToken);
        if (activeDeposit?.Status == PaymentStatus.PAID)
        {
            return BadRequestDetail(OrderErrorCodes.DepositAlreadyPaid, "Deposit payment has already been paid.");
        }

        var reusableFromCreated = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
            _payments,
            _unitOfWork,
            activeDeposit,
            cancellationToken);
        if (reusableFromCreated is not null && ActivePaymentResolver.IsActive(reusableFromCreated, DateTime.UtcNow))
        {
            var existingDetail = await _payments.GetDetailAsync(reusableFromCreated.PaymentId, cancellationToken);
            return ServiceResult<PaymentDetailDto>.Success(
                PaymentServiceActivePaymentSupport.ToDetailDto(existingDetail, reusableFromCreated, reused: true),
                "Active payment retrieved successfully.");
        }

        var paymentCode = await GenerateUniquePaymentCodeAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            QuotationId = order.QuotationId,
            PaymentCode = paymentCode,
            PaidBy = order.CustomerId,
            PaymentType = PaymentType.DEPOSIT,
            Amount = depositAmount,
            Currency = _sePayOptions.Currency,
            Status = PaymentStatus.PENDING,
            ExpiredAt = request.ExpiredAt,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _payments.AddPaymentAsync(payment, cancellationToken);

        if (order.Status == OrderStatus.CREATED)
        {
            var orderEntity = await _orders.GetByIdAsync(orderId, cancellationToken);
            if (orderEntity is not null)
            {
                orderEntity.Status = OrderStatus.DEPOSIT_PENDING;
                orderEntity.UpdatedAt = now;
                _orders.Update(orderEntity);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentCreated,
            payment,
            cancellationToken: cancellationToken);

        var detail = await _payments.GetDetailAsync(payment.PaymentId, cancellationToken);
        return ServiceResult<PaymentDetailDto>.Created(
            detail?.Adapt<PaymentDetailDto>() ?? payment.Adapt<PaymentDetailDto>(),
            "Deposit payment created successfully.");
    }

    public async Task<ServiceResult<PaymentDetailDto>> CreateRemainingPaymentForOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderRemainingPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentDetailDto>.Unauthorized();
        }

        var order = await _orders.GetDetailAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFoundDetail(OrderErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanManageAsAssignedSales(
                role,
                order.AssignedSalesId,
                currentUserId))
        {
            return ServiceResult<PaymentDetailDto>.Forbidden("You do not have permission to create this remaining payment.");
        }

        if (order.Status != OrderStatus.FINAL_PAYMENT_PENDING)
        {
            return BadRequestDetail(
                OrderErrorCodes.OrderNotReadyForRemainingPayment,
                "Order is not ready for remaining payment.");
        }

        var remainingAmount = order.RemainingAmount ?? 0m;
        if (remainingAmount <= 0m)
        {
            return BadRequestDetail(
                OrderErrorCodes.RemainingPaymentNotRequired,
                "Remaining payment is not required.");
        }

        var existing = await _payments.GetByOrderAndTypeAsync(orderId, PaymentType.REMAINING_PAYMENT, cancellationToken);
        if (existing?.Status == PaymentStatus.PAID)
        {
            return BadRequestDetail(
                OrderErrorCodes.RemainingPaymentAlreadyPaid,
                "Remaining payment has already been paid.");
        }

        var reusable = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
            _payments,
            _unitOfWork,
            existing,
            cancellationToken);
        if (reusable is not null && ActivePaymentResolver.IsActive(reusable, DateTime.UtcNow))
        {
            var existingDetail = await _payments.GetDetailAsync(reusable.PaymentId, cancellationToken);
            return ServiceResult<PaymentDetailDto>.Success(
                PaymentServiceActivePaymentSupport.ToDetailDto(existingDetail, reusable, reused: true),
                "Active payment retrieved successfully.");
        }

        var paymentCode = await GenerateUniquePaymentCodeAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            QuotationId = order.QuotationId,
            PaymentCode = paymentCode,
            PaidBy = order.CustomerId,
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = remainingAmount,
            Currency = _sePayOptions.Currency,
            Status = PaymentStatus.PENDING,
            ExpiredAt = request.ExpiredAt,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _payments.AddPaymentAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentCreated,
            payment,
            cancellationToken: cancellationToken);

        var detail = await _payments.GetDetailAsync(payment.PaymentId, cancellationToken);
        return ServiceResult<PaymentDetailDto>.Created(
            detail?.Adapt<PaymentDetailDto>() ?? payment.Adapt<PaymentDetailDto>(),
            "Remaining payment created successfully.");
    }

    public async Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectStartFeePaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentDetailDto>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return BadRequestDetail(PaymentErrorCodes.ProjectNotFound, "Project id is required.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(PaymentErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectStartFeeAccessEvaluator.CanManage(role, project.AssignedSalesId, currentUserId))
        {
            return ServiceResult<PaymentDetailDto>.Forbidden("You do not have permission to create this project start fee payment.");
        }

        if (project.AssignedDesignerId.HasValue)
        {
            return BadRequestDetail(
                PaymentErrorCodes.DesignerAlreadyAssigned,
                "Designer has already been assigned to this project.");
        }

        if (!ProjectStartFeeRules.IsProjectStatusEligibleForPaymentCreation(project.Status))
        {
            return BadRequestDetail(
                ProjectStatusErrorCodes.InvalidProjectStatus,
                "Project status is not eligible for project start fee payment.");
        }

        var existing = await _payments.GetByProjectAndTypeAsync(
            projectId,
            PaymentType.PROJECT_START_FEE,
            cancellationToken);
        if (existing?.Status == PaymentStatus.PAID)
        {
            return BadRequestDetail(
                PaymentErrorCodes.ProjectStartFeeAlreadyPaid,
                "Project start fee has already been paid.");
        }

        var reusable = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
            _payments,
            _unitOfWork,
            existing,
            cancellationToken);
        if (reusable is not null && ActivePaymentResolver.IsActive(reusable, DateTime.UtcNow))
        {
            var existingDetail = await _payments.GetDetailAsync(reusable.PaymentId, cancellationToken);
            return ServiceResult<PaymentDetailDto>.Success(
                PaymentServiceActivePaymentSupport.ToDetailDto(existingDetail, reusable, reused: true),
                "Active payment retrieved successfully.");
        }

        var amount = request.Amount ?? _projectWorkflowSettings.DefaultProjectStartFeeAmount;
        if (amount <= 0m)
        {
            return BadRequestDetail(PaymentErrorCodes.InvalidPaymentAmount, "Amount must be greater than zero.");
        }

        var paymentCode = await GenerateUniquePaymentCodeAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            OrderId = null,
            QuotationId = null,
            PaymentCode = paymentCode,
            PaidBy = project.CustomerId,
            PaymentType = PaymentType.PROJECT_START_FEE,
            Amount = amount,
            Currency = _sePayOptions.Currency,
            Status = PaymentStatus.PENDING,
            ExpiredAt = request.ExpiredAt,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _payments.AddPaymentAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentCreated,
            payment,
            cancellationToken: cancellationToken);

        var detail = await _payments.GetDetailAsync(payment.PaymentId, cancellationToken);
        return ServiceResult<PaymentDetailDto>.Created(
            detail?.Adapt<PaymentDetailDto>() ?? payment.Adapt<PaymentDetailDto>(),
            "Project start fee payment created successfully.");
    }

    public async Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectStartFeeStatusDto>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectStartFeeStatusDto>.BadRequest("Project id is required.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound<ProjectStartFeeStatusDto>(PaymentErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectStartFeeAccessEvaluator.CanManage(role, project.AssignedSalesId, currentUserId))
        {
            return ServiceResult<ProjectStartFeeStatusDto>.Forbidden(
                "You do not have permission to view project start fee status.");
        }

        var payment = await _payments.GetByProjectAndTypeAsync(
            projectId,
            PaymentType.PROJECT_START_FEE,
            cancellationToken);

        return ServiceResult<ProjectStartFeeStatusDto>.Success(
            ProjectStartFeeRules.BuildStatus(projectId, payment),
            "Project start fee status retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentDetailDto>.Unauthorized();
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var payment = await SyncPaymentExpiryAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFoundDetail(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        return ServiceResult<PaymentDetailDto>.Success(
            await BuildPaymentDetailDtoAsync(detail, payment, cancellationToken),
            "Customer payment detail retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
        Guid currentUserId,
        PaymentQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentListResponseDto>.Unauthorized();
        }

        var paginationError = PaymentServiceManagementSupport.ValidatePagination(query.Page, query.PageSize);
        if (paginationError is not null)
        {
            return Failure<PaymentListResponseDto>(
                PaymentErrorCodes.InvalidPaymentFilter,
                paginationError,
                isBadRequest: true);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (query.ProjectId.HasValue)
        {
            var project = await _projects.GetDetailAsync(query.ProjectId.Value, cancellationToken);
            if (project is null)
            {
                return ServiceResult<PaymentListResponseDto>.NotFound(ProjectNotFoundMessage);
            }

            if (!CanAccessProject(role, project.CustomerId, project.AssignedSalesId, project.AssignedDesignerId, currentUserId))
            {
                return ServiceResult<PaymentListResponseDto>.Forbidden("You do not have access to this project's payments.");
            }
        }

        var readQuery = PaymentServiceManagementSupport.BuildScopedQuery(query, role, currentUserId);
        await SyncExpiredPaymentsInScopeAsync(readQuery, cancellationToken);

        var items = await _payments.GetListAsync(readQuery, cancellationToken);
        var totalItems = await _payments.CountAsync(readQuery, cancellationToken);
        var successPaymentIds = await _payments.GetPaymentIdsWithSuccessfulTransactionAsync(
            items.Select(item => item.PaymentId).ToList(),
            cancellationToken);
        var utcNow = DateTime.UtcNow;
        var responseItems = items
            .Select(item => PaymentServiceManagementSupport.ToListItemDto(
                item,
                successPaymentIds.Contains(item.PaymentId),
                utcNow))
            .ToList();
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return ServiceResult<PaymentListResponseDto>.Success(
            new PaymentListResponseDto
            {
                Items = responseItems,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            },
            "Customer payments retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentSummaryResponseDto>> GetSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentSummaryResponseDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is ProjectAssignmentAccessEvaluator.DesignerRole)
        {
            return ServiceResult<PaymentSummaryResponseDto>.Forbidden(
                "You do not have permission to view payment summary.");
        }

        var readQuery = PaymentServiceManagementSupport.BuildScopeOnly(role, currentUserId);
        await SyncExpiredPaymentsInScopeAsync(readQuery, cancellationToken);

        var summary = await _payments.GetSummaryAsync(readQuery, DateTime.UtcNow, cancellationToken);
        return ServiceResult<PaymentSummaryResponseDto>.Success(
            new PaymentSummaryResponseDto
            {
                PendingCount = summary.PendingCount,
                ProcessingCount = summary.ProcessingCount,
                PaidCount = summary.PaidCount,
                ExpiredCount = summary.ExpiredCount,
                CancelledCount = summary.CancelledCount,
                PayableCount = summary.PayableCount,
                PendingAmount = summary.PayablePendingAmount,
                Currency = _sePayOptions.Currency
            },
            "Customer payment summary retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentTransactionListResponseDto>.Unauthorized();
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<PaymentTransactionListResponseDto>.NotFound(PaymentNotFoundMessage);
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<PaymentTransactionListResponseDto>.Forbidden(accessError.Message ?? ForbiddenMessage);
        }

        var items = await _payments.GetTransactionsByPaymentIdAsync(paymentId, cancellationToken);
        return ServiceResult<PaymentTransactionListResponseDto>.Success(
            new PaymentTransactionListResponseDto { Items = items.Adapt<List<PaymentTransactionDto>>() },
            "Payment transactions retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
        string paymentCode,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentStatusByCodeDto>.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(paymentCode))
        {
            return ServiceResult<PaymentStatusByCodeDto>.BadRequest("Payment code is required.");
        }

        var detail = await _payments.GetDetailByPaymentCodeAsync(paymentCode.Trim(), cancellationToken);
        if (detail is null)
        {
            return NotFound<PaymentStatusByCodeDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<PaymentStatusByCodeDto>.Forbidden(accessError.Message ?? ForbiddenMessage);
        }

        var status = await _payments.GetStatusByPaymentCodeAsync(paymentCode.Trim(), cancellationToken);
        return ServiceResult<PaymentStatusByCodeDto>.Success(
            status!.Adapt<PaymentStatusByCodeDto>(),
            "Payment status retrieved successfully.");
    }

    public async Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<SePayVietQrResponseDto>.Unauthorized();
        }

        if (!_sePayOptions.Enabled || !_sePayOptions.VietQrEnabled)
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.SePayDisabled,
                "SePay VietQR is disabled.",
                isBadRequest: true);
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return NotFound<SePayVietQrResponseDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<SePayVietQrResponseDto>.Forbidden(accessError.Message ?? ForbiddenMessage);
        }

        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFound<SePayVietQrResponseDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var stateError = ValidateVietQrState(payment);
        if (stateError is not null)
        {
            return stateError;
        }

        return ServiceResult<SePayVietQrResponseDto>.Success(
            new SePayVietQrResponseDto
            {
                PaymentId = payment.PaymentId,
                PaymentCode = payment.PaymentCode,
                Amount = payment.Amount,
                BankCode = _sePayOptions.BankCode,
                AccountNo = _sePayOptions.BankAccountNo,
                AccountName = _sePayOptions.BankAccountName,
                TransferContent = payment.PaymentCode,
                VietQrUrl = _vietQrUrlBuilder.Build(payment),
                Status = payment.Status
            },
            "SePay VietQR generated successfully.");
    }

    public async Task<bool> CanAccessPaymentAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return false;
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return false;
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        return accessError is null;
    }

    public async Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
        Guid paymentId,
        Guid currentUserId,
        CreatePayOsPaymentLinkRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PayOsPaymentLinkResponseDto>.Unauthorized();
        }

        if (!_payOsOptions.Enabled)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.PayOsDisabled,
                "PayOS is disabled.",
                isBadRequest: true);
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return NotFound<PayOsPaymentLinkResponseDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var accessError = await ValidateAccessAsync(detail, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<PayOsPaymentLinkResponseDto>.Forbidden(accessError.Message ?? ForbiddenMessage);
        }

        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFound<PayOsPaymentLinkResponseDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var stateValidation = PaymentCollectableStateValidator.Validate(payment);
        if (!stateValidation.IsValid)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                stateValidation.ErrorCode!,
                stateValidation.ErrorMessage!,
                isBadRequest: true);
        }

        if (await _payments.HasSuccessfulTransactionAsync(payment.PaymentId, cancellationToken))
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.PaymentAlreadyPaid,
                "Payment has already been paid.",
                isBadRequest: true);
        }

        var amount = payment.Amount;
        if (amount <= 0m)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Amount must be greater than zero.",
                isBadRequest: true);
        }

        var payOsAmount = ToPayOsAmount(amount);
        if (payOsAmount is null)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Amount exceeds PayOS supported range.",
                isBadRequest: true);
        }

        var orderCode = await GenerateUniquePayOsOrderCodeAsync(cancellationToken);
        var orderCodeText = orderCode.ToString(CultureInfo.InvariantCulture);
        var returnUrl = ResolveUrl(request.ReturnUrl, _payOsOptions.ReturnUrl);
        var cancelUrl = ResolveUrl(request.CancelUrl, _payOsOptions.CancelUrl);

        if (string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(cancelUrl))
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.PayOsCreateLinkFailed,
                "PayOS return and cancel URLs must be configured.",
                isBadRequest: true);
        }

        var now = DateTime.UtcNow;
        var transaction = new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            OrderId = payment.OrderId,
            TransactionCode = await GenerateUniqueTransactionCodeAsync(cancellationToken),
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = amount,
            Currency = payment.Currency,
            PaymentProvider = PaymentProvider.PAYOS,
            PaymentMethod = PaymentMethod.PAYMENT_LINK,
            ProviderReferenceCode = orderCodeText,
            Status = PaymentTransactionStatus.PENDING,
            CreatedAt = now
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payments.AddTransactionAsync(transaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            PayOsCreatePaymentLinkResult payOsResult;
            try
            {
                payOsResult = await _payOsClient.CreatePaymentLinkAsync(
                    new PayOsCreatePaymentLinkRequest
                    {
                        OrderCode = orderCode,
                        Amount = payOsAmount.Value,
                        Description = BuildPayOsDescription(payment.PaymentCode),
                        ReturnUrl = returnUrl,
                        CancelUrl = cancelUrl
                    },
                    cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Failure<PayOsPaymentLinkResponseDto>(
                    PaymentErrorCodes.PayOsCreateLinkFailed,
                    "Failed to create PayOS payment link.",
                    isBadRequest: true);
            }

            if (string.IsNullOrWhiteSpace(payOsResult.CheckoutUrl))
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Failure<PayOsPaymentLinkResponseDto>(
                    PaymentErrorCodes.PayOsCreateLinkFailed,
                    "PayOS did not return a checkout URL.",
                    isBadRequest: true);
            }

            transaction.ProviderTransactionId = payOsResult.PaymentLinkId;
            transaction.PaymentUrl = payOsResult.CheckoutUrl;
            transaction.QrContent = payOsResult.QrCode;
            PaymentSummaryCalculator.MarkProcessing(payment, now);
            _payments.UpdateTransaction(transaction);
            _payments.UpdatePayment(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ServiceResult<PayOsPaymentLinkResponseDto>.Success(
                new PayOsPaymentLinkResponseDto
                {
                    PaymentId = payment.PaymentId,
                    PaymentTransactionId = transaction.PaymentTransactionId,
                    PaymentCode = payment.PaymentCode,
                    OrderCode = orderCode,
                    Amount = amount,
                    Status = transaction.Status,
                    PaymentStatus = payment.Status,
                    CheckoutUrl = payOsResult.CheckoutUrl,
                    QrCode = payOsResult.QrCode
                },
                "PayOS payment link created successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
        Guid paymentId,
        Guid currentUserId,
        CreatePaymentTransactionAttemptRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var preparation = await PrepareCustomerPaymentAttemptAsync(
            paymentId,
            currentUserId,
            cancellationToken);
        if (preparation.Error is not null)
        {
            return preparation.Error;
        }

        var payment = preparation.Payment!;

        if (await _payments.HasSuccessfulTransactionAsync(paymentId, cancellationToken))
        {
            return Failure<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.PaymentAlreadyPaid,
                "Payment has already been paid.",
                isBadRequest: true);
        }

        if (request.PaymentProvider == PaymentProvider.PAYOS &&
            request.PaymentMethod == PaymentMethod.PAYMENT_LINK)
        {
            return await CreatePayOsTransactionAttemptAsync(
                paymentId,
                currentUserId,
                payment,
                request,
                cancellationToken);
        }

        if (request.PaymentProvider == PaymentProvider.SEPAY &&
            request.PaymentMethod == PaymentMethod.QR_CODE)
        {
            return await CreateSePayTransactionAttemptAsync(
                paymentId,
                payment,
                cancellationToken);
        }

        if (request.PaymentProvider != PaymentProvider.PAYOS && request.PaymentProvider != PaymentProvider.SEPAY)
        {
            return Failure<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.UnsupportedPaymentProvider,
                "Unsupported payment provider.",
                isBadRequest: true);
        }

        return Failure<PaymentTransactionAttemptResponseDto>(
            PaymentErrorCodes.UnsupportedPaymentMethod,
            "Unsupported payment method.",
            isBadRequest: true);
    }

    private async Task<(ServiceResult<PaymentTransactionAttemptResponseDto>? Error, Payment? Payment)> PrepareCustomerPaymentAttemptAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return (ServiceResult<PaymentTransactionAttemptResponseDto>.Unauthorized(), null);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ProjectAssignmentAccessEvaluator.CustomerRole)
        {
            return (ServiceResult<PaymentTransactionAttemptResponseDto>.Forbidden(
                "Only customers can create online payment attempts."), null);
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return (NotFound<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.PaymentNotFound,
                PaymentNotFoundMessage), null);
        }

        if (!PaymentServiceManagementSupport.IsCustomerOwner(detail, currentUserId))
        {
            return (ServiceResult<PaymentTransactionAttemptResponseDto>.Forbidden(
                PaymentAccessForbiddenMessage), null);
        }

        var payment = await SyncPaymentExpiryAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return (NotFound<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.PaymentNotFound,
                PaymentNotFoundMessage), null);
        }

        var stateValidation = PaymentCollectableStateValidator.Validate(payment);
        if (!stateValidation.IsValid)
        {
            return (Failure<PaymentTransactionAttemptResponseDto>(
                stateValidation.ErrorCode!,
                stateValidation.ErrorMessage!,
                isBadRequest: true), null);
        }

        return (null, payment);
    }

    private async Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePayOsTransactionAttemptAsync(
        Guid paymentId,
        Guid currentUserId,
        Payment payment,
        CreatePaymentTransactionAttemptRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!PaymentServiceManagementSupport.IsValidHttpsUrl(request.ReturnUrl) ||
            !PaymentServiceManagementSupport.IsValidHttpsUrl(request.CancelUrl))
        {
            return Failure<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.PayOsCreateLinkFailed,
                "PayOS return and cancel URLs must be valid HTTPS URLs.",
                isBadRequest: true);
        }

        var reusablePayOs = await _payments.GetLatestPendingTransactionAsync(
            paymentId,
            PaymentProvider.PAYOS,
            PaymentMethod.PAYMENT_LINK,
            cancellationToken);
        if (IsReusablePendingTransaction(reusablePayOs))
        {
            return ServiceResult<PaymentTransactionAttemptResponseDto>.Success(
                PaymentServiceManagementSupport.ToAttemptResponse(reusablePayOs!, payment),
                PaymentTransactionAttemptCreatedMessage);
        }

        var linkResult = await CreatePayOsPaymentLinkAsync(
            paymentId,
            currentUserId,
            new CreatePayOsPaymentLinkRequestDto
            {
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl
            },
            cancellationToken);

        if (linkResult.Status is not (200 or 201) || linkResult.Data is null)
        {
            return new ServiceResult<PaymentTransactionAttemptResponseDto>(
                linkResult.Status,
                linkResult.Message ?? "Failed to create payment transaction attempt.")
            {
                ErrorCode = linkResult.ErrorCode
            };
        }

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentProcessing,
            payment,
            cancellationToken: cancellationToken);

        return ServiceResult<PaymentTransactionAttemptResponseDto>.Success(
            new PaymentTransactionAttemptResponseDto
            {
                PaymentTransactionId = linkResult.Data.PaymentTransactionId,
                PaymentId = linkResult.Data.PaymentId,
                TransactionCode = string.Empty,
                Amount = linkResult.Data.Amount,
                Currency = payment.Currency,
                Status = linkResult.Data.Status,
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK,
                PaymentUrl = linkResult.Data.CheckoutUrl,
                QrContent = linkResult.Data.QrCode,
                PaymentStatus = linkResult.Data.PaymentStatus
            },
            PaymentTransactionAttemptCreatedMessage);
    }

    private async Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreateSePayTransactionAttemptAsync(
        Guid paymentId,
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (!_sePayOptions.Enabled || !_sePayOptions.VietQrEnabled)
        {
            return Failure<PaymentTransactionAttemptResponseDto>(
                PaymentErrorCodes.SePayDisabled,
                "SePay VietQR is disabled.",
                isBadRequest: true);
        }

        var reusableSePay = await _payments.GetLatestPendingTransactionAsync(
            paymentId,
            PaymentProvider.SEPAY,
            PaymentMethod.QR_CODE,
            cancellationToken);
        if (IsReusablePendingTransaction(reusableSePay))
        {
            return ServiceResult<PaymentTransactionAttemptResponseDto>.Success(
                PaymentServiceManagementSupport.ToAttemptResponse(reusableSePay!, payment),
                PaymentTransactionAttemptCreatedMessage);
        }

        var now = DateTime.UtcNow;
        var transaction = new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            OrderId = payment.OrderId,
            TransactionCode = await GenerateUniqueTransactionCodeAsync(cancellationToken),
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentProvider = PaymentProvider.SEPAY,
            PaymentMethod = PaymentMethod.QR_CODE,
            Status = PaymentTransactionStatus.PENDING,
            CreatedAt = now
        };

        transaction.PaymentUrl = _vietQrUrlBuilder.Build(payment);
        PaymentSummaryCalculator.MarkProcessing(payment, now);

        await _payments.AddTransactionAsync(transaction, cancellationToken);
        _payments.UpdatePayment(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentProcessing,
            payment,
            cancellationToken: cancellationToken);

        var createdTransaction = new PaymentTransactionReadModel
        {
            PaymentTransactionId = transaction.PaymentTransactionId,
            PaymentId = transaction.PaymentId,
            TransactionCode = transaction.TransactionCode,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            PaymentProvider = transaction.PaymentProvider,
            PaymentMethod = transaction.PaymentMethod,
            PaymentUrl = transaction.PaymentUrl
        };

        return ServiceResult<PaymentTransactionAttemptResponseDto>.Success(
            PaymentServiceManagementSupport.ToAttemptResponse(createdTransaction, payment),
            PaymentTransactionAttemptCreatedMessage);
    }

    public async Task<ServiceResult<PaymentTransactionDto?>> GetActiveTransactionAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentTransactionDto?>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ProjectAssignmentAccessEvaluator.CustomerRole)
        {
            return ServiceResult<PaymentTransactionDto?>.Forbidden(
                "Only customers can view active payment attempts.");
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<PaymentTransactionDto?>.NotFound(PaymentNotFoundMessage);
        }

        if (!PaymentServiceManagementSupport.IsCustomerOwner(detail, currentUserId))
        {
            return ServiceResult<PaymentTransactionDto?>.Forbidden(
                PaymentAccessForbiddenMessage);
        }

        var payment = await SyncPaymentExpiryAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return ServiceResult<PaymentTransactionDto?>.NotFound(PaymentNotFoundMessage);
        }

        if (!PaymentPayableEvaluator.IsPayable(
                payment,
                await _payments.HasSuccessfulTransactionAsync(paymentId, cancellationToken),
                DateTime.UtcNow))
        {
            return ServiceResult<PaymentTransactionDto?>.Success(
                null,
                "Active payment transaction retrieved successfully.");
        }

        var activeTransaction = await _payments.GetLatestPendingTransactionAsync(
            paymentId,
            PaymentProvider.PAYOS,
            PaymentMethod.PAYMENT_LINK,
            cancellationToken);
        if (!IsReusablePendingTransaction(activeTransaction))
        {
            activeTransaction = await _payments.GetLatestPendingTransactionAsync(
                paymentId,
                PaymentProvider.SEPAY,
                PaymentMethod.QR_CODE,
                cancellationToken);
        }

        if (!IsReusablePendingTransaction(activeTransaction))
        {
            return ServiceResult<PaymentTransactionDto?>.Success(
                null,
                "Active payment transaction retrieved successfully.");
        }

        return ServiceResult<PaymentTransactionDto?>.Success(
            activeTransaction!.Adapt<PaymentTransactionDto>(),
            "Active payment transaction retrieved successfully.");
    }

    public async Task<ServiceResult<PaymentTransactionDto>> CancelTransactionAsync(
        Guid paymentId,
        Guid paymentTransactionId,
        Guid currentUserId,
        CancelPaymentTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PaymentTransactionDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ProjectAssignmentAccessEvaluator.CustomerRole)
        {
            return ServiceResult<PaymentTransactionDto>.Forbidden(
                "Only customers can cancel payment attempts.");
        }

        var detail = await _payments.GetDetailAsync(paymentId, cancellationToken);
        if (detail is null)
        {
            return NotFound<PaymentTransactionDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        if (!PaymentServiceManagementSupport.IsCustomerOwner(detail, currentUserId))
        {
            return ServiceResult<PaymentTransactionDto>.Forbidden(
                PaymentAccessForbiddenMessage);
        }

        var transaction = await _payments.GetTransactionByIdAsync(paymentTransactionId, cancellationToken);
        if (transaction is null || transaction.PaymentId != paymentId)
        {
            return NotFound<PaymentTransactionDto>(
                PaymentErrorCodes.PaymentTransactionNotFound,
                "Payment transaction not found.");
        }

        if (transaction.Status == PaymentTransactionStatus.SUCCESS)
        {
            return Failure<PaymentTransactionDto>(
                PaymentErrorCodes.SuccessTransactionCannotBeCancelled,
                "Successful payment transactions cannot be cancelled.",
                isBadRequest: true);
        }

        if (transaction.Status == PaymentTransactionStatus.CANCELLED)
        {
            return ServiceResult<PaymentTransactionDto>.Success(
                transaction.Adapt<PaymentTransactionDto>(),
                "Payment transaction cancelled successfully.");
        }

        if (transaction.Status != PaymentTransactionStatus.PENDING)
        {
            return Failure<PaymentTransactionDto>(
                PaymentErrorCodes.PaymentTransactionNotCancellable,
                "Payment transaction cannot be cancelled.",
                isBadRequest: true);
        }

        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFound<PaymentTransactionDto>(PaymentErrorCodes.PaymentNotFound, PaymentNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        transaction.Status = PaymentTransactionStatus.CANCELLED;
        if (!string.IsNullOrWhiteSpace(request.CancelReason))
        {
            transaction.FailureReason = request.CancelReason.Trim();
        }

        var hasOtherPending = await HasOtherPendingTransactionsAsync(
            paymentId,
            paymentTransactionId,
            cancellationToken);
        if (!hasOtherPending)
        {
            PaymentSummaryCalculator.RevertToPendingIfCollectable(payment, now);
            _payments.UpdatePayment(payment);
        }

        _payments.UpdateTransaction(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentTransactionCancelled,
            payment,
            cancellationToken: cancellationToken);

        return ServiceResult<PaymentTransactionDto>.Success(
            transaction.Adapt<PaymentTransactionDto>(),
            "Payment transaction cancelled successfully.");
    }

    public async Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
        PayOsConfirmWebhookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_payOsOptions.Enabled)
        {
            return Failure<PayOsConfirmWebhookResponseDto>(
                PaymentErrorCodes.PayOsDisabled,
                "PayOS is disabled.",
                isBadRequest: true);
        }

        var webhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl)
            ? _payOsOptions.WebhookUrl
            : request.WebhookUrl.Trim();

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return Failure<PayOsConfirmWebhookResponseDto>(
                PaymentErrorCodes.PayOsCreateLinkFailed,
                "Webhook URL is required.",
                isBadRequest: true);
        }

        try
        {
            var confirmedUrl = await _payOsClient.ConfirmWebhookAsync(webhookUrl, cancellationToken);
            return ServiceResult<PayOsConfirmWebhookResponseDto>.Success(
                new PayOsConfirmWebhookResponseDto
                {
                    Success = true,
                    WebhookUrl = confirmedUrl
                },
                "PayOS webhook URL confirmed successfully.");
        }
        catch
        {
            return Failure<PayOsConfirmWebhookResponseDto>(
                PaymentErrorCodes.PayOsCreateLinkFailed,
                "Failed to confirm PayOS webhook URL.",
                isBadRequest: true);
        }
    }

    private async Task<string> GenerateUniquePaymentCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPaymentCodeAttempts; attempt++)
        {
            var code = PaymentCodeGenerator.Generate(_sePayOptions.PaymentCodePrefix, _sePayOptions.PaymentCodeRandomDigits);
            if (!await _payments.PaymentCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique payment code.");
    }

    private async Task<long> GenerateUniquePayOsOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPayOsOrderCodeAttempts; attempt++)
        {
            var orderCode = PayOsOrderCodeGenerator.Generate();
            var orderCodeText = orderCode.ToString(CultureInfo.InvariantCulture);
            if (!await _payments.PayOsOrderCodeExistsAsync(orderCodeText, cancellationToken))
            {
                return orderCode;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique PayOS order code.");
    }

    private async Task<string> GenerateUniqueTransactionCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTransactionCodeAttempts; attempt++)
        {
            var code = TransactionCodeGenerator.Generate();
            if (!await _payments.TransactionCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique transaction code.");
    }

    private string BuildPayOsDescription(string paymentCode)
    {
        var description = $"{_payOsOptions.DescriptionPrefix}{paymentCode}";
        if (description.Length <= _payOsOptions.MaxDescriptionLength)
        {
            return description;
        }

        return description[.._payOsOptions.MaxDescriptionLength];
    }

    private static int? ToPayOsAmount(decimal amount)
    {
        var truncated = decimal.Truncate(amount);
        if (truncated <= 0m || truncated > int.MaxValue)
        {
            return null;
        }

        return (int)truncated;
    }

    private static string ResolveUrl(string? requestedUrl, string configuredUrl)
    {
        return string.IsNullOrWhiteSpace(requestedUrl) ? configuredUrl : requestedUrl.Trim();
    }

    private async Task<PaymentDetailDto> BuildPaymentDetailDtoAsync(
        PaymentDetailReadModel detail,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var hasSuccessfulTransaction = await _payments.HasSuccessfulTransactionAsync(
            payment.PaymentId,
            cancellationToken);
        var dto = detail.Adapt<PaymentDetailDto>();
        dto.IsPayable = PaymentPayableEvaluator.IsPayable(payment, hasSuccessfulTransaction, DateTime.UtcNow);

        var project = await _projects.GetDetailAsync(payment.ProjectId, cancellationToken);
        if (project is not null)
        {
            dto.Project = new PaymentProjectSummaryDto
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName
            };
        }

        if (payment.OrderId.HasValue)
        {
            var order = await _orders.GetDetailAsync(payment.OrderId.Value, cancellationToken);
            if (order is not null)
            {
                dto.Order = new PaymentOrderSummaryDto
                {
                    OrderId = order.OrderId,
                    OrderCode = order.OrderCode,
                    Status = order.Status,
                    FinalTotalAmount = order.FinalTotalAmount,
                    DepositAmount = order.DepositAmount,
                    PaidAmount = order.PaidAmount,
                    RemainingAmount = order.RemainingAmount
                };
            }
        }

        var latestTransaction = await _payments.GetLatestTransactionAsync(payment.PaymentId, cancellationToken);
        dto.LatestTransaction = PaymentServiceManagementSupport.ToLatestTransactionDto(latestTransaction);
        return dto;
    }

    private async Task<Payment?> SyncPaymentExpiryAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        if (!PaymentExpirySynchronizer.TryMarkExpiredIfNeeded(payment, DateTime.UtcNow))
        {
            return payment;
        }

        _payments.UpdatePayment(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            _notifications,
            _logger,
            NotificationType.PaymentExpired,
            payment,
            cancellationToken: cancellationToken);
        return payment;
    }

    private async Task SyncExpiredPaymentsInScopeAsync(
        PaymentQueryReadModel scope,
        CancellationToken cancellationToken)
    {
        var expiredPayments = await _payments.GetExpiredPaymentsForSyncAsync(
            scope,
            DateTime.UtcNow,
            cancellationToken);
        if (expiredPayments.Count == 0)
        {
            return;
        }

        foreach (var payment in expiredPayments)
        {
            PaymentExpirySynchronizer.TryMarkExpiredIfNeeded(payment, DateTime.UtcNow);
            _payments.UpdatePayment(payment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var payment in expiredPayments)
        {
            await PaymentCustomerNotificationSupport.TryDispatchAsync(
                _notifications,
                _logger,
                NotificationType.PaymentExpired,
                payment,
                cancellationToken: cancellationToken);
        }
    }

    private async Task<bool> HasOtherPendingTransactionsAsync(
        Guid paymentId,
        Guid excludedTransactionId,
        CancellationToken cancellationToken)
    {
        var transactions = await _payments.GetTransactionsByPaymentIdAsync(paymentId, cancellationToken);
        return transactions.Any(transaction =>
            transaction.PaymentTransactionId != excludedTransactionId &&
            transaction.Status == PaymentTransactionStatus.PENDING);
    }

    private static bool IsReusablePendingTransaction(PaymentTransactionReadModel? transaction)
    {
        return transaction is not null &&
            transaction.Status == PaymentTransactionStatus.PENDING &&
            !string.IsNullOrWhiteSpace(transaction.PaymentUrl);
    }

    private async Task<ServiceResult<PaymentDetailDto>?> ValidateAccessAsync(
        PaymentDetailReadModel detail,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        return CanAccessProject(
                role,
                detail.CustomerId,
                detail.AssignedSalesId,
                detail.AssignedDesignerId,
                currentUserId)
            ? null
            : ServiceResult<PaymentDetailDto>.Forbidden(PaymentAccessForbiddenMessage);
    }

    private static bool CanAccessProject(
        string? role,
        Guid customerId,
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId)
    {
        return ProjectAssignmentAccessEvaluator.CanAccessProjectAssignment(
            role,
            customerId,
            assignedSalesId,
            assignedDesignerId,
            currentUserId);
    }

    private static ServiceResult<SePayVietQrResponseDto>? ValidateVietQrState(Payment payment)
    {
        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.PAID or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.InvalidPaymentStatus,
                "Payment is not eligible for VietQR generation.",
                isBadRequest: true);
        }

        if (!payment.Status.HasValue || !VietQrEligibleStatuses.Contains(payment.Status.Value))
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.InvalidPaymentStatus,
                "Payment is not eligible for VietQR generation.",
                isBadRequest: true);
        }

        if (payment.ExpiredAt.HasValue && payment.ExpiredAt.Value <= DateTime.UtcNow)
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.PaymentExpired,
                "Payment has expired.",
                isBadRequest: true);
        }

        if (payment.Amount <= 0m)
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Payment amount must be greater than zero.",
                isBadRequest: true);
        }

        return null;
    }

    private static ServiceResult<PaymentDetailDto> NotFoundDetail(string code, string message)
    {
        return NotFound<PaymentDetailDto>(code, message);
    }

    private static ServiceResult<PaymentDetailDto> BadRequestDetail(string code, string message)
    {
        return Failure<PaymentDetailDto>(code, message, isBadRequest: true);
    }

    private static ServiceResult<T> NotFound<T>(string code, string message)
    {
        return ServiceResult<T>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<T> Failure<T>(string code, string message, bool isBadRequest)
    {
        return ServiceResult<T>.Failure(
            isBadRequest ? Error.BadRequest(code, message) : Error.NotFound(code, message));
    }
}
