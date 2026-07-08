#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
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
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "CUSTOMER",
            OrderDetail = CreateOrderDetail(OrderStatus.DEPOSIT_PENDING),
            Payments = repository
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
    public async Task CreateRemainingPaymentForOrderAsync_WhenValid_CreatesPayment()
    {
        var repository = new PaymentServiceFakeRepository();
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

        Assert.Equal(201, result.Status);
        Assert.Equal(PaymentType.REMAINING_PAYMENT, result.Data!.PaymentType);
        Assert.Equal(70m, result.Data.Amount);
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
        var service = BuildService(new PaymentServiceTestOptions
        {
            Role = "SALES",
            ProjectDetail = CreateProjectDetail(),
            Payments = repository,
            DefaultProjectStartFeeAmount = 500000m
        });

        var result = await service.CreateProjectStartFeePaymentAsync(
            _projectId,
            _salesId,
            new CreateProjectStartFeePaymentRequestDto { Note = "Start fee" });

        Assert.Equal(201, result.Status);
        Assert.Equal(PaymentType.PROJECT_START_FEE, result.Data!.PaymentType);
        Assert.Equal(500000m, result.Data.Amount);
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
            PaidAmount = 0m,
            RemainingAmount = 30m,
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
            OrderDetail = options.OrderDetail
        };
        var payOsClient = new PaymentServiceFakePayOsClient();
        var saveChangesCount = 0;
        var unitOfWork = TestUnitOfWork.ForSaveChanges(_ =>
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
                ReturnUrl = "https://example.com/return",
                CancelUrl = "https://example.com/cancel",
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

        return new PaymentService(payments, projects, orders, dependencies);
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

    private PaymentDetailReadModel CreatePaymentDetail(Guid paymentId, string paymentCode = "FS12345678")
    {
        return new PaymentDetailReadModel
        {
            PaymentId = paymentId,
            ProjectId = _projectId,
            PaymentCode = paymentCode,
            Amount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 30m,
            Currency = "VND",
            Status = PaymentStatus.PENDING,
            CustomerId = _customerId,
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
            PaidAmount = status == PaymentStatus.PAID ? amount : 0m,
            RemainingAmount = status == PaymentStatus.PAID ? 0m : amount,
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
        public PaymentServiceFakeRepository? Payments { get; init; }
        public bool SePayEnabled { get; init; }
        public bool VietQrEnabled { get; init; }
        public bool PayOsEnabled { get; init; } = true;
        public decimal DefaultProjectStartFeeAmount { get; init; } = 500000m;
    }
}
