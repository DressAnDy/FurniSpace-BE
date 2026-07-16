using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Identity;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Payments;
using static FurniSpace.Application.Constants.Payments.PaymentServiceConstants;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
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

    public PaymentService(
        IPaymentRepository payments,
        IProjectRepository projects,
        IOrderRepository orders,
        PaymentServiceDependencies dependencies)
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
            PaidAmount = 0m,
            RemainingAmount = request.Amount,
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

        if (order.Status != OrderStatus.DEPOSIT_PENDING)
        {
            return BadRequestDetail(OrderErrorCodes.InvalidOrderStatus, "Order is not pending deposit payment.");
        }

        var depositAmount = order.DepositAmount ?? 0m;
        if (depositAmount <= 0m)
        {
            return BadRequestDetail(PaymentErrorCodes.InvalidPaymentAmount, "Deposit amount must be greater than zero.");
        }

        var existing = await _payments.GetByOrderAndTypeAsync(orderId, PaymentType.DEPOSIT, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.PAID)
            {
                return BadRequestDetail(OrderErrorCodes.DepositAlreadyPaid, "Deposit payment has already been paid.");
            }

            if (existing.Status.HasValue && CollectableDepositStatuses.Contains(existing.Status.Value))
            {
                var existingDetail = await _payments.GetDetailAsync(existing.PaymentId, cancellationToken);
                return ServiceResult<PaymentDetailDto>.Success(
                    existingDetail?.Adapt<PaymentDetailDto>() ?? existing.Adapt<PaymentDetailDto>(),
                    "Existing deposit payment returned.");
            }
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
            PaidAmount = 0m,
            RemainingAmount = depositAmount,
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
        if (!OrderAccessEvaluator.CanManageDepositPayment(
                role,
                order.CustomerId,
                order.AssignedSalesId,
                currentUserId))
        {
            return ServiceResult<PaymentDetailDto>.Forbidden("You do not have permission to create this remaining payment.");
        }

        if (order.Status != OrderStatus.FINAL_PAYMENT_PENDING)
        {
            return BadRequestDetail(OrderErrorCodes.InvalidOrderStatus, "Order is not pending final payment.");
        }

        var remainingAmount = order.RemainingAmount ?? 0m;
        if (remainingAmount <= 0m)
        {
            return BadRequestDetail(PaymentErrorCodes.InvalidPaymentAmount, "Remaining amount must be greater than zero.");
        }

        var existing = await _payments.GetByOrderAndTypeAsync(orderId, PaymentType.REMAINING_PAYMENT, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.PAID)
            {
                return BadRequestDetail(
                    OrderErrorCodes.RemainingPaymentAlreadyPaid,
                    "Remaining payment has already been paid.");
            }

            if (existing.Status.HasValue && CollectableDepositStatuses.Contains(existing.Status.Value))
            {
                var existingDetail = await _payments.GetDetailAsync(existing.PaymentId, cancellationToken);
                return ServiceResult<PaymentDetailDto>.Success(
                    existingDetail?.Adapt<PaymentDetailDto>() ?? existing.Adapt<PaymentDetailDto>(),
                    "Existing remaining payment returned.");
            }
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
            PaidAmount = 0m,
            RemainingAmount = remainingAmount,
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
        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.PAID)
            {
                return BadRequestDetail(
                    PaymentErrorCodes.ProjectStartFeeAlreadyPaid,
                    "Project start fee has already been paid.");
            }

            if (existing.Status.HasValue &&
                ProjectStartFeeRules.CollectablePaymentStatuses.Contains(existing.Status.Value))
            {
                var existingDetail = await _payments.GetDetailAsync(existing.PaymentId, cancellationToken);
                return ServiceResult<PaymentDetailDto>.Success(
                    existingDetail?.Adapt<PaymentDetailDto>() ?? existing.Adapt<PaymentDetailDto>(),
                    "Existing project start fee payment returned.");
            }
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
            PaidAmount = 0m,
            RemainingAmount = amount,
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

        return ServiceResult<PaymentDetailDto>.Success(
            detail.Adapt<PaymentDetailDto>(),
            "Payment retrieved successfully.");
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

        if (query.ProjectId.HasValue)
        {
            var project = await _projects.GetDetailAsync(query.ProjectId.Value, cancellationToken);
            if (project is null)
            {
                return ServiceResult<PaymentListResponseDto>.NotFound(ProjectNotFoundMessage);
            }

            var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
            if (!CanAccessProject(role, project.CustomerId, project.AssignedSalesId, project.AssignedDesignerId, currentUserId))
            {
                return ServiceResult<PaymentListResponseDto>.Forbidden("You do not have access to this project's payments.");
            }
        }

        var readQuery = query.Adapt<PaymentQueryReadModel>();
        var items = await _payments.GetListAsync(readQuery, cancellationToken);
        return ServiceResult<PaymentListResponseDto>.Success(
            new PaymentListResponseDto { Items = items.Adapt<List<PaymentDto>>() },
            "Payments retrieved successfully.");
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
                Amount = payment.RemainingAmount,
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

        var amount = request.Amount ?? payment.RemainingAmount;
        if (amount <= 0m)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Amount must be greater than zero.",
                isBadRequest: true);
        }

        if (amount > payment.RemainingAmount)
        {
            return Failure<PayOsPaymentLinkResponseDto>(
                PaymentErrorCodes.PaymentAmountExceedsRemaining,
                "Amount exceeds remaining payment amount.",
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
            _payments.UpdateTransaction(transaction);
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
            : ServiceResult<PaymentDetailDto>.Forbidden("You do not have access to this payment.");
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

        if (payment.RemainingAmount <= 0m)
        {
            return Failure<SePayVietQrResponseDto>(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Payment has no remaining amount.",
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
