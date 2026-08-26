#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.Projects;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentServiceTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WithEmptyUser_ReturnsUnauthorized()
    {
        var service = BuildService();

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            Guid.Empty,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenOrderMissing_ReturnsNotFound()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "CUSTOMER" });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenForbidden_ReturnsForbidden()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING, customerId: Guid.NewGuid())
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenValid_CreatesPayment()
    {
        var repository = new PaymentServiceFakeRepository();
        var dispatcher = new PaymentServiceFakeNotificationDispatcher();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
            Payments = repository,
            Notifications = dispatcher
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto { Note = "Deposit" });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(PaymentType.DEPOSIT, result.Data!.PaymentType);
        Assert.Equal(30m, result.Data.Amount);
        Assert.Single(repository.NewPayments);
        Assert.Equal(NotificationType.PaymentCreated, Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenCollectableExists_ReturnsExistingPayment()
    {
        var paymentId = Guid.NewGuid();
        var existing = CreatePayment(
            paymentId,
            PaymentType.DEPOSIT,
            PaymentStatus.PENDING,
            30m,
            orderId: _orderId);
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(existing);
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
            Payments = repository
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(paymentId, result.Data!.PaymentId);
        Assert.Empty(repository.NewPayments);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenDeliveryDetailsMissing_ReturnsRequiredError()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.CREATED),
            OrderEntity = CreateOrderEntity(
                CreateOrderDetail(OrderStatus.CREATED),
                deliveryAddress: null)
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderDeliveryDetailsRequired, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenReusablePaymentExistsButReceiverMissing_ReturnsRequiredError()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(CreatePayment(
            paymentId,
            PaymentType.DEPOSIT,
            PaymentStatus.PENDING,
            30m,
            orderId: _orderId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
            OrderEntity = CreateOrderEntity(
                CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
                receiverPhone: " "),
            Payments = repository
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderDeliveryDetailsRequired, result.ErrorCode);
        Assert.Empty(repository.NewPayments);
    }

    [Fact]
    public async Task CreateRemainingPaymentForOrderAsync_WhenValid_CreatesPayment()
    {
        var repository = new PaymentServiceFakeRepository();
        var dispatcher = new PaymentServiceFakeNotificationDispatcher();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateOrderDetail(OrderStatus.FINAL_PAYMENT_PENDING, remainingAmount: 70m),
            Payments = repository,
            Notifications = dispatcher
        });

        var result = await service.CreateRemainingPaymentForOrderAsync(
            _orderId,
            _salesId,
            new CreateOrderRemainingPaymentRequestDto());

        Assert.Equal(201, result.Status);
        Assert.Equal(PaymentType.REMAINING_PAYMENT, result.Data!.PaymentType);
        Assert.Equal(70m, result.Data.Amount);
        Assert.Equal(NotificationType.PaymentCreated, Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateRemainingPaymentForOrderAsync_WhenActivePaymentExists_ReturnsExistingPayment()
    {
        var paymentId = Guid.NewGuid();
        var existing = CreatePayment(
            paymentId,
            PaymentType.REMAINING_PAYMENT,
            PaymentStatus.PENDING,
            70m,
            orderId: _orderId);
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(existing, CreatePaymentDetail(paymentId, amount: 70m));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateOrderDetail(OrderStatus.FINAL_PAYMENT_PENDING, remainingAmount: 70m),
            Payments = repository
        });

        var result = await service.CreateRemainingPaymentForOrderAsync(
            _orderId,
            _salesId,
            new CreateOrderRemainingPaymentRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(paymentId, result.Data!.PaymentId);
        Assert.True(result.Data.Reused);
        Assert.Empty(repository.NewPayments);
    }

    [Theory]
    [InlineData(OrderStatus.DELIVERED, 70, OrderErrorCodes.OrderNotReadyForRemainingPayment)]
    [InlineData(OrderStatus.FINAL_PAYMENT_PENDING, 0, OrderErrorCodes.RemainingPaymentNotRequired)]
    public async Task CreateRemainingPaymentForOrderAsync_WhenInvalid_ReturnsExpectedBadRequest(
        OrderStatus status,
        decimal remainingAmount,
        string expectedCode)
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateOrderDetail(status, remainingAmount: remainingAmount)
        });

        var result = await service.CreateRemainingPaymentForOrderAsync(
            _orderId,
            _salesId,
            new CreateOrderRemainingPaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Fact]
    public async Task CreateRemainingPaymentForOrderAsync_WhenCustomerCalls_ReturnsForbidden()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.FINAL_PAYMENT_PENDING, remainingAmount: 70m)
        });

        var result = await service.CreateRemainingPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderRemainingPaymentRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateProjectStartFeePaymentAsync_WhenDesignerAssigned_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = CreateProjectDetail(hasDesigner: true)
        });

        var result = await service.CreateProjectStartFeePaymentAsync(
            _projectId,
            _salesId,
            new CreateProjectStartFeePaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.DesignerAlreadyAssigned, result.ErrorCode);
    }

    [Fact]
    public async Task CreateProjectStartFeePaymentAsync_WhenValid_CreatesPayment()
    {
        var repository = new PaymentServiceFakeRepository();
        var dispatcher = new PaymentServiceFakeNotificationDispatcher();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = CreateProjectDetail(),
            Payments = repository,
            DefaultProjectStartFeeAmount = 500000m,
            Notifications = dispatcher
        });

        var result = await service.CreateProjectStartFeePaymentAsync(
            _projectId,
            _salesId,
            new CreateProjectStartFeePaymentRequestDto { Note = "Start fee" });

        Assert.Equal(201, result.Status);
        Assert.Equal(PaymentType.PROJECT_START_FEE, result.Data!.PaymentType);
        Assert.Equal(500000m, result.Data.Amount);
        Assert.Equal(NotificationType.PaymentCreated, Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateProjectStartFeePaymentAsync_WhenExpiryInPast_ReturnsValidationError()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = CreateProjectDetail()
        });

        var result = await service.CreateProjectStartFeePaymentAsync(
            _projectId,
            _salesId,
            new CreateProjectStartFeePaymentRequestDto
            {
                ExpiredAt = DateTime.UtcNow.AddMinutes(-1)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PaymentExpired, result.ErrorCode);
    }

    [Fact]
    public async Task CreateProjectStartFeePaymentAsync_WhenExpiryExceedsTarget_ReturnsValidationError()
    {
        var projectDetail = CreateProjectDetail();
        projectDetail.TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(10);
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = projectDetail
        });

        var result = await service.CreateProjectStartFeePaymentAsync(
            _projectId,
            _salesId,
            new CreateProjectStartFeePaymentRequestDto
            {
                ExpiredAt = DateTime.UtcNow.AddDays(15)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.ProjectStartFeeExpiryExceedsTarget, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectStartFeeStatusAsync_WhenAuthorized_ReturnsStatus()
    {
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(CreatePayment(
            Guid.NewGuid(),
            PaymentType.PROJECT_START_FEE,
            PaymentStatus.PAID,
            500000m));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = CreateProjectDetail(),
            Payments = repository
        });

        var result = await service.GetProjectStartFeeStatusAsync(_projectId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentStatus.PAID, result.Data!.ProjectStartFeeStatus);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentMissing_ReturnsNotFound()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "CUSTOMER" });

        var result = await service.GetByIdAsync(Guid.NewGuid(), _customerId);

        Assert.Equal(404, result.Status);
        Assert.Equal(PaymentErrorCodes.PaymentNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAuthorized_ReturnsPayment()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetByIdAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(paymentId, result.Data!.PaymentId);
    }

    [Fact]
    public async Task GetListAsync_WhenProjectMissing_ReturnsNotFound()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "ADMIN" });

        var result = await service.GetListAsync(
            _customerId,
            new PaymentQueryDto { ProjectId = _projectId });

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetListAsync_WhenAuthorized_ReturnsItems()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedListItem(new PaymentListItemReadModel
        {
            PaymentId = paymentId,
            ProjectId = _projectId,
            PaymentCode = "FS12345678",
            Amount = 30m,
            Status = PaymentStatus.PENDING
        });
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Payments = repository
        });

        var result = await service.GetListAsync(
            _customerId,
            new PaymentQueryDto { ProjectId = _projectId });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetTransactionsAsync_WhenAuthorized_ReturnsTransactions()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        await repository.AddTransactionAsync(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionCode = "TXN-001",
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        });
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetTransactionsAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetStatusByCodeAsync_WhenAuthorized_ReturnsStatus()
    {
        const string paymentCode = "FS12345678";
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m, paymentCode: paymentCode),
            CreatePaymentDetail(paymentId, paymentCode));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetStatusByCodeAsync(paymentCode, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(paymentCode, result.Data!.PaymentCode);
    }

    [Fact]
    public async Task GenerateSePayVietQrAsync_WhenDisabled_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            SePayEnabled = false
        });

        var result = await service.GenerateSePayVietQrAsync(paymentId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.SePayDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task GenerateSePayVietQrAsync_WhenValid_ReturnsVietQr()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            SePayEnabled = true,
            VietQrEnabled = true
        });

        var result = await service.GenerateSePayVietQrAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(paymentId, result.Data!.PaymentId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.VietQrUrl));
    }

    [Fact]
    public async Task CanAccessPaymentAsync_WhenAuthorized_ReturnsTrue()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var canAccess = await service.CanAccessPaymentAsync(paymentId, _customerId);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CreatePayOsPaymentLinkAsync_WhenValid_ReturnsCheckoutUrl()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true
        });

        var result = await service.CreatePayOsPaymentLinkAsync(
            paymentId,
            _customerId,
            new CreatePayOsPaymentLinkRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal("https://pay.payos.vn/checkout", result.Data!.CheckoutUrl);
    }

    [Fact]
    public async Task CreateTestPaymentAsync_WhenValid_CreatesPayment()
    {
        var repository = new PaymentServiceFakeRepository();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "ADMIN",
            ProjectDetail = CreateProjectDetail(),
            Payments = repository
        });

        var result = await service.CreateTestPaymentAsync(
            _salesId,
            new CreateTestPaymentRequestDto
            {
                ProjectId = _projectId,
                Amount = 10000m,
                PaymentType = PaymentType.PROJECT_START_FEE
            });

        Assert.Equal(201, result.Status);
        Assert.Single(repository.NewPayments);
    }

    [Fact]
    public async Task GetByIdAsync_WhenForbidden_ReturnsForbidden()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId, customerId: Guid.NewGuid()));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetByIdAsync(paymentId, _customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreatePayOsPaymentLinkAsync_WhenDisabled_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = false
        });

        var result = await service.CreatePayOsPaymentLinkAsync(
            paymentId,
            _customerId,
            new CreatePayOsPaymentLinkRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmPayOsWebhookAsync_WhenDisabled_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions { PayOsEnabled = false });

        var result = await service.ConfirmPayOsWebhookAsync(new PayOsConfirmWebhookRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenInvalidStatus_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PAID)
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidOrderStatus, result.ErrorCode);
    }

    [Fact]
    public async Task CanAccessPaymentAsync_WhenUnauthorized_ReturnsFalse()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId, customerId: Guid.NewGuid()));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var canAccess = await service.CanAccessPaymentAsync(paymentId, _customerId);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task GetListAsync_WhenProjectForbidden_ReturnsForbidden()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail()
        });

        var result = await service.GetListAsync(
            Guid.NewGuid(),
            new PaymentQueryDto { ProjectId = _projectId });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreatePayOsPaymentLinkAsync_WhenPayOsFails_RollsBackAndReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var rollbackCalled = false;
        var payOsClient = new PaymentServiceFakePayOsClient { ShouldFail = true };
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true,
            PayOsClient = payOsClient,
            UnitOfWork = TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => Task.FromResult(1),
                _ => Task.CompletedTask,
                _ =>
                {
                    rollbackCalled = true;
                    return Task.CompletedTask;
                })
        });

        var result = await service.CreatePayOsPaymentLinkAsync(
            paymentId,
            _customerId,
            new CreatePayOsPaymentLinkRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsCreateLinkFailed, result.ErrorCode);
        Assert.True(rollbackCalled);
    }

    [Fact]
    public async Task CreatePayOsPaymentLinkAsync_WhenCheckoutUrlMissing_RollsBackAndReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        var payOsClient = new PaymentServiceFakePayOsClient
        {
            Result = new PayOsCreatePaymentLinkResult { CheckoutUrl = string.Empty, PaymentLinkId = "plink-001" }
        };
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true,
            PayOsClient = payOsClient,
            UnitOfWork = TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => Task.FromResult(1),
                _ => Task.CompletedTask,
                _ => Task.CompletedTask)
        });

        var result = await service.CreatePayOsPaymentLinkAsync(
            paymentId,
            _customerId,
            new CreatePayOsPaymentLinkRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsCreateLinkFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreatePayOsPaymentLinkAsync_WhenPaymentAlreadyPaid_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m),
            CreatePaymentDetail(paymentId));
        await repository.AddTransactionAsync(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionCode = "TXN-PAID",
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.SUCCESS,
            CreatedAt = DateTime.UtcNow
        });
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true
        });

        var result = await service.CreatePayOsPaymentLinkAsync(
            paymentId,
            _customerId,
            new CreatePayOsPaymentLinkRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PaymentAlreadyPaid, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmPayOsWebhookAsync_WhenValid_ReturnsConfirmedUrl()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            PayOsEnabled = true,
            PayOsClient = new PaymentServiceFakePayOsClient()
        });

        var result = await service.ConfirmPayOsWebhookAsync(new PayOsConfirmWebhookRequestDto
        {
            WebhookUrl = "https://example.com/webhook"
        });

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.Success);
        Assert.Equal("https://example.com/webhook", result.Data.WebhookUrl);
    }

    [Fact]
    public async Task ConfirmPayOsWebhookAsync_WhenConfirmFails_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            PayOsEnabled = true,
            PayOsClient = new FailingConfirmPayOsClient()
        });

        var result = await service.ConfirmPayOsWebhookAsync(new PayOsConfirmWebhookRequestDto
        {
            WebhookUrl = "https://example.com/webhook"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsCreateLinkFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateTestPaymentAsync_WhenProjectMissing_ReturnsNotFound()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "ADMIN" });

        var result = await service.CreateTestPaymentAsync(
            _salesId,
            new CreateTestPaymentRequestDto
            {
                ProjectId = _projectId,
                Amount = 10000m,
                PaymentType = PaymentType.PROJECT_START_FEE
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(PaymentErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateTestPaymentAsync_WhenAmountInvalid_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "ADMIN",
            ProjectDetail = CreateProjectDetail()
        });

        var result = await service.CreateTestPaymentAsync(
            _salesId,
            new CreateTestPaymentRequestDto
            {
                ProjectId = _projectId,
                Amount = 0m,
                PaymentType = PaymentType.PROJECT_START_FEE
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.InvalidPaymentAmount, result.ErrorCode);
    }

    [Fact]
    public async Task GenerateSePayVietQrAsync_WhenPaymentPaid_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(
            CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PAID, 30m),
            CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            SePayEnabled = true,
            VietQrEnabled = true
        });

        var result = await service.GenerateSePayVietQrAsync(paymentId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.InvalidPaymentStatus, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDepositPaymentForOrderAsync_WhenDepositAlreadyPaid_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(CreatePayment(
            paymentId,
            PaymentType.DEPOSIT,
            PaymentStatus.PAID,
            30m,
            orderId: _orderId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
            Payments = repository
        });

        var result = await service.CreateDepositPaymentForOrderAsync(
            _orderId,
            _customerId,
            new CreateOrderDepositPaymentRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DepositAlreadyPaid, result.ErrorCode);
    }

    [Fact]
    public async Task GetStatusByCodeAsync_WhenCodeMissing_ReturnsBadRequest()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "CUSTOMER" });

        var result = await service.GetStatusByCodeAsync("  ", _customerId);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CanAccessPaymentAsync_WhenPaymentMissing_ReturnsFalse()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "CUSTOMER" });

        var canAccess = await service.CanAccessPaymentAsync(Guid.NewGuid(), _customerId);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenAuthorized_ReturnsSummary()
    {
        var repository = new PaymentServiceFakeRepository
        {
            Summary = new PaymentSummaryReadModel
            {
                PendingCount = 2,
                PaidCount = 1,
                PayableCount = 2,
                PayablePendingAmount = 130m
            }
        };
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetSummaryAsync(_customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.PendingCount);
        Assert.Equal(130m, result.Data.PendingAmount);
        Assert.Equal("VND", result.Data.Currency);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenDesigner_ReturnsForbidden()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "DESIGNER" });

        var result = await service.GetSummaryAsync(_customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_SePayQr_CreatesPendingTransaction()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.PaidBy = _customerId;
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId, customerId: _customerId));
        var dispatcher = new PaymentServiceFakeNotificationDispatcher();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            SePayEnabled = true,
            VietQrEnabled = true,
            Notifications = dispatcher
        });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            paymentId,
            _customerId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.SEPAY,
                PaymentMethod = PaymentMethod.QR_CODE
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentProvider.SEPAY, result.Data!.PaymentProvider);
        Assert.Equal(PaymentTransactionStatus.PENDING, result.Data.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.PaymentUrl));
        Assert.Equal(PaymentStatus.PROCESSING, payment.Status);
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_UnsupportedProvider_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.PaidBy = _customerId;
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId, customerId: _customerId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            paymentId,
            _customerId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = (PaymentProvider)999,
                PaymentMethod = PaymentMethod.QR_CODE
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.UnsupportedPaymentProvider, result.ErrorCode);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_WhenSalesRole_ReturnsForbidden()
    {
        var service = BuildService(new PaymentServiceTestOptions { Role = "SALES" });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            Guid.NewGuid(),
            _salesId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.SEPAY,
                PaymentMethod = PaymentMethod.QR_CODE
            });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_PayOs_WithoutRequestUrls_UsesConfiguredUrls()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.PaidBy = _customerId;
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId, customerId: _customerId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true
        });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            paymentId,
            _customerId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentProvider.PAYOS, result.Data!.PaymentProvider);
        Assert.Equal("https://pay.payos.vn/checkout", result.Data.PaymentUrl);
        Assert.Equal("qr-data", result.Data.QrContent);
        Assert.Equal(PaymentStatus.PROCESSING, payment.Status);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_PayOs_WithoutUrlsAndNoConfig_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.PaidBy = _customerId;
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId, customerId: _customerId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true,
            PayOsReturnUrl = string.Empty,
            PayOsCancelUrl = string.Empty
        });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            paymentId,
            _customerId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsCreateLinkFailed, result.ErrorCode);
        Assert.Equal("PayOS return and cancel URLs must be configured.", result.Message);
    }

    [Fact]
    public async Task CreatePaymentTransactionAttemptAsync_PayOs_WithHttpRequestUrls_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.PaidBy = _customerId;
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId, customerId: _customerId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            PayOsEnabled = true
        });

        var result = await service.CreatePaymentTransactionAttemptAsync(
            paymentId,
            _customerId,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK,
                ReturnUrl = "http://example.com/return",
                CancelUrl = "http://example.com/cancel"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(PaymentErrorCodes.PayOsCreateLinkFailed, result.ErrorCode);
        Assert.Equal("PayOS return and cancel URLs must be valid HTTPS URLs.", result.Message);
    }

    [Fact]
    public async Task GetActiveTransactionAsync_WhenPendingSePayExists_ReturnsTransaction()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PROCESSING, 30m);
        payment.PaidBy = _customerId;
        var detail = CreatePaymentDetail(paymentId, customerId: _customerId);
        detail.PaidBy = _customerId;
        repository.SeedPayment(payment, detail);
        await repository.AddTransactionAsync(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionCode = "TXN-SEPAY",
            Amount = 30m,
            Currency = "VND",
            PaymentProvider = PaymentProvider.SEPAY,
            PaymentMethod = PaymentMethod.QR_CODE,
            Status = PaymentTransactionStatus.PENDING,
            PaymentUrl = "https://vietqr.test",
            CreatedAt = DateTime.UtcNow
        });
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetActiveTransactionAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("TXN-SEPAY", result.Data!.TransactionCode);
    }

    [Fact]
    public async Task GetActiveTransactionAsync_WhenPaymentNotPayable_ReturnsNullData()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PAID, 30m);
        payment.PaidBy = _customerId;
        var detail = CreatePaymentDetail(paymentId, customerId: _customerId);
        detail.PaidBy = _customerId;
        detail.Status = PaymentStatus.PAID;
        repository.SeedPayment(payment, detail);
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetActiveTransactionAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CancelTransactionAsync_WhenPending_CancelsTransactionAndRevertsPayment()
    {
        var paymentId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PROCESSING, 30m);
        payment.PaidBy = _customerId;
        var detail = CreatePaymentDetail(paymentId, customerId: _customerId);
        detail.PaidBy = _customerId;
        repository.SeedPayment(payment, detail);
        await repository.AddTransactionAsync(new PaymentTransaction
        {
            PaymentTransactionId = transactionId,
            PaymentId = paymentId,
            TransactionCode = "TXN-CANCEL",
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        });
        var dispatcher = new PaymentServiceFakeNotificationDispatcher();
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository,
            Notifications = dispatcher
        });

        var result = await service.CancelTransactionAsync(
            paymentId,
            transactionId,
            _customerId,
            new CancelPaymentTransactionRequestDto { CancelReason = "Changed mind" });

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentTransactionStatus.CANCELLED, result.Data!.Status);
        Assert.Equal(PaymentStatus.PENDING, payment.Status);
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CancelTransactionAsync_WhenAlreadyCancelled_IsIdempotent()
    {
        var paymentId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PROCESSING, 30m);
        payment.PaidBy = _customerId;
        var detail = CreatePaymentDetail(paymentId, customerId: _customerId);
        detail.PaidBy = _customerId;
        repository.SeedPayment(payment, detail);
        await repository.AddTransactionAsync(new PaymentTransaction
        {
            PaymentTransactionId = transactionId,
            PaymentId = paymentId,
            TransactionCode = "TXN-CANCELLED",
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.CANCELLED,
            CreatedAt = DateTime.UtcNow
        });
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.CancelTransactionAsync(
            paymentId,
            transactionId,
            _customerId,
            new CancelPaymentTransactionRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentTransactionStatus.CANCELLED, result.Data!.Status);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExpiredPayment_SyncsExpiredStatus()
    {
        var paymentId = Guid.NewGuid();
        var repository = new PaymentServiceFakeRepository();
        var payment = CreatePayment(paymentId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m);
        payment.ExpiredAt = DateTime.UtcNow.AddMinutes(-10);
        repository.SeedPayment(payment, CreatePaymentDetail(paymentId));
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            Payments = repository
        });

        var result = await service.GetByIdAsync(paymentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(PaymentStatus.EXPIRED, payment.Status);
    }

    private static PaymentService BuildService(PaymentServiceTestOptions? options = null)
    {
        options ??= new PaymentServiceTestOptions();
        var payments = options.Payments ?? new PaymentServiceFakeRepository();
        var projects = new PaymentServiceFakeProjectRepository
        {
            ProjectDetail = options.ProjectDetail,
            Role = options.Role
        };
        var orders = new PaymentServiceFakeOrderRepository
        {
            OrderDetail = options.OrderDetail,
            OrderEntity = options.OrderEntity ??
                (options.OrderDetail is null ? null : CreateOrderEntity(options.OrderDetail))
        };
        var payOsClient = options.PayOsClient ?? new PaymentServiceFakePayOsClient();
        var saveChangesCount = 0;
        var unitOfWork = options.UnitOfWork ?? TestUnitOfWork.ForSaveChanges(_ =>
        {
            saveChangesCount++;
            return Task.FromResult(1);
        });

        var dependencies = new PaymentServiceDependencies(
            unitOfWork,
            new SePayOptions
            {
                Enabled = options.SePayEnabled,
                VietQrEnabled = options.VietQrEnabled,
                Currency = "VND",
                PaymentCodePrefix = "FS",
                PaymentCodeRandomDigits = 8,
                BankCode = "MB",
                BankAccountNo = "1017588888",
                BankAccountName = "FurniSpace"
            },
            new PayOsOptions
            {
                Enabled = options.PayOsEnabled,
                ReturnUrl = options.PayOsReturnUrl ?? "https://example.com/return",
                CancelUrl = options.PayOsCancelUrl ?? "https://example.com/cancel",
                DescriptionPrefix = "FS "
            },
            new ProjectWorkflowSettings
            {
                DefaultProjectStartFeeAmount = options.DefaultProjectStartFeeAmount
            },
            new SePayVietQrUrlBuilder(Options.Create(new SePayOptions
            {
                BankCode = "MB",
                BankAccountNo = "1017588888",
                BankAccountName = "FurniSpace"
            })),
            payOsClient);

        return new PaymentService(payments, projects, orders, dependencies, options.Notifications);
    }

    private OrderDetailReadModel CreateOrderDetail(
        OrderStatus status,
        Guid? customerId = null,
        decimal depositAmount = 30m,
        decimal remainingAmount = 70m)
    {
        return new OrderDetailReadModel
        {
            OrderId = _orderId,
            ProjectId = _projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = customerId ?? _customerId,
            SalesId = _salesId,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m,
            DepositAmount = depositAmount,
            PaidAmount = 100m - remainingAmount,
            RemainingAmount = remainingAmount,
            Status = status,
            AssignedSalesId = _salesId
        };
    }

    private static Order CreateOrderEntity(
        OrderDetailReadModel detail,
        string? deliveryAddress = "123 Nguyen Trai",
        string? receiverName = "Nguyen Van A",
        string? receiverPhone = "0901234567")
    {
        return new Order
        {
            OrderId = detail.OrderId,
            ProjectId = detail.ProjectId,
            QuotationId = detail.QuotationId,
            CustomerId = detail.CustomerId,
            SalesId = detail.SalesId,
            Status = detail.Status,
            DeliveryAddress = deliveryAddress,
            ReceiverName = receiverName,
            ReceiverPhone = receiverPhone
        };
    }

    private ProjectDetailReadModel CreateProjectDetail(bool hasDesigner = false)
    {
        return new ProjectDetailReadModel
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = hasDesigner ? Guid.NewGuid() : null,
            Status = ProjectStatus.IN_CONSULTATION
        };
    }

    private PaymentDetailReadModel CreatePaymentDetail(
        Guid paymentId,
        string paymentCode = "FS12345678",
        Guid? customerId = null,
        decimal amount = 30m)
    {
        return new PaymentDetailReadModel
        {
            PaymentId = paymentId,
            ProjectId = _projectId,
            PaymentCode = paymentCode,
            Amount = amount,
            Currency = "VND",
            Status = PaymentStatus.PENDING,
            CustomerId = customerId ?? _customerId,
            PaidBy = customerId ?? _customerId,
            AssignedSalesId = _salesId
        };
    }

    private Payment CreatePayment(
        Guid paymentId,
        PaymentType paymentType,
        PaymentStatus status,
        decimal amount,
        string paymentCode = "FS12345678",
        Guid? orderId = null)
    {
        return new Payment
        {
            PaymentId = paymentId,
            ProjectId = _projectId,
            OrderId = orderId,
            PaymentCode = paymentCode,
            PaidBy = _customerId,
            PaymentType = paymentType,
            Amount = amount,
            Currency = "VND",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class PaymentServiceTestOptions
    {
        public string Role { get; init; } = "ADMIN";
        public ProjectDetailReadModel? ProjectDetail { get; init; }
        public OrderDetailReadModel? OrderDetail { get; init; }
        public Order? OrderEntity { get; init; }
        public PaymentServiceFakeRepository? Payments { get; init; }
        public IPayOsClient? PayOsClient { get; init; }
        public IUnitOfWork? UnitOfWork { get; init; }
        public bool SePayEnabled { get; init; }
        public bool VietQrEnabled { get; init; }
        public bool PayOsEnabled { get; init; } = true;
        public string? PayOsReturnUrl { get; init; }
        public string? PayOsCancelUrl { get; init; }
        public decimal DefaultProjectStartFeeAmount { get; init; } = 500000m;
        public INotificationDispatcher? Notifications { get; init; }
    }

    private sealed class PaymentServiceFakeNotificationDispatcher : INotificationDispatcher
    {
        public List<NotificationType> Dispatched { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            Dispatched.Add(type);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingConfirmPayOsClient : IPayOsClient
    {
        public Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
            PayOsCreatePaymentLinkRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PayOsCreatePaymentLinkResult());

        public Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(
            string rawBody,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PayOsVerifiedWebhookData());

        public Task<string> ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Confirm failed.");
    }
}
