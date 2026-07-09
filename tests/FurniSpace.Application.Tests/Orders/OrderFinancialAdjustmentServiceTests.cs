#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderFinancialAdjustmentServiceTests
{
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();

    [Fact]
    public async Task UpdateFinancialAdjustmentAsync_Customer_ReturnsForbidden()
    {
        var service = BuildService(new TestOptions { Role = "CUSTOMER" });

        var result = await service.UpdateFinancialAdjustmentAsync(
            _orderId,
            _customerId,
            CreateRequest(5_000_000m, 25_000_000m));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateFinancialAdjustmentAsync_WhenDepositPartiallyPaid_ReturnsOrderPaymentAlreadyStarted()
    {
        var service = BuildService(new TestOptions
        {
            Role = "SALES",
            DepositPayment = CreateDepositPayment(PaymentStatus.PARTIALLY_PAID, paidAmount: 10_000_000m)
        });

        var result = await service.UpdateFinancialAdjustmentAsync(
            _orderId,
            _salesId,
            CreateRequest(5_000_000m, 25_000_000m));

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderPaymentAlreadyStarted, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateFinancialAdjustmentAsync_WhenDepositPending_UpdatesOrderAndPayment()
    {
        var depositPayment = CreateDepositPayment(PaymentStatus.PENDING, paidAmount: 0m);
        depositPayment.Amount = 30_000_000m;
        depositPayment.RemainingAmount = 30_000_000m;
        var options = new TestOptions
        {
            Role = "SALES",
            DepositPayment = depositPayment
        };
        var service = BuildService(options);

        var result = await service.UpdateFinancialAdjustmentAsync(
            _orderId,
            _salesId,
            CreateRequest(5_000_000m, 25_000_000m, "Final discount approved by Sales Manager."));

        Assert.Equal(200, result.Status);
        Assert.Equal(95_000_000m, result.Data!.FinalTotalAmount);
        Assert.Equal(5_000_000m, result.Data.AdditionalDiscountAmount);
        Assert.Equal(25_000_000m, result.Data.DepositAmount);
        Assert.Equal(95_000_000m, result.Data.RemainingAmount);
        Assert.NotNull(options.Order);
        Assert.Equal(100_000_000m, options.Order.OriginalTotalAmount);
        Assert.NotNull(options.DepositPayment);
        Assert.Equal(25_000_000m, options.DepositPayment.Amount);
        Assert.Equal(25_000_000m, options.DepositPayment.RemainingAmount);
        Assert.Equal("Final discount approved by Sales Manager.", options.DepositPayment.Note);
        Assert.Equal(1, options.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateFinancialAdjustmentAsync_WhenDiscountTooHigh_ReturnsInvalidFinancialAdjustment()
    {
        var service = BuildService(new TestOptions { Role = "ADMIN" });

        var result = await service.UpdateFinancialAdjustmentAsync(
            _orderId,
            Guid.NewGuid(),
            CreateRequest(100_000_000m, 25_000_000m));

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidFinancialAdjustment, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateFinancialAdjustmentAsync_WhenOrderNotDepositPending_ReturnsInvalidOrderStatus()
    {
        var service = BuildService(new TestOptions
        {
            Role = "SALES",
            OrderStatus = OrderStatus.DEPOSIT_PAID
        });

        var result = await service.UpdateFinancialAdjustmentAsync(
            _orderId,
            _salesId,
            CreateRequest(5_000_000m, 25_000_000m));

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidOrderStatus, result.ErrorCode);
    }

    private static UpdateOrderFinancialAdjustmentRequestDto CreateRequest(
        decimal additionalDiscountAmount,
        decimal depositAmount,
        string? adjustmentNote = null) => new()
    {
        AdditionalDiscountAmount = additionalDiscountAmount,
        DepositAmount = depositAmount,
        AdjustmentNote = adjustmentNote
    };

    private OrderService BuildService(TestOptions options)
    {
        options.Order ??= CreateOrder(options.OrderStatus);
        options.OrderDetail ??= CreateDetail(options.Order);
        options.ProjectDetail ??= CreateProjectDetail();
        options.UnitOfWork ??= new TrackingUnitOfWork();
        options.PaymentRepository ??= new TrackingPaymentRepository(options.DepositPayment);

        return new OrderService(
            new TrackingOrderRepository(options.Order, options.OrderDetail),
            new FakeProjectRepository(options.ProjectDetail, options.Role),
            options.PaymentRepository,
            options.UnitOfWork);
    }

    private Order CreateOrder(OrderStatus status) => new()
    {
        OrderId = _orderId,
        ProjectId = _projectId,
        QuotationId = Guid.NewGuid(),
        OrderCode = "ORD-001",
        CustomerId = _customerId,
        SalesId = _salesId,
        OriginalTotalAmount = 100_000_000m,
        ItemAdjustmentAmount = 0m,
        AdditionalDiscountAmount = 0m,
        FinalTotalAmount = 100_000_000m,
        DepositAmount = 30_000_000m,
        PaidAmount = 0m,
        RemainingAmount = 100_000_000m,
        Status = status
    };

    private OrderDetailReadModel CreateDetail(Order order) => new()
    {
        OrderId = order.OrderId,
        ProjectId = order.ProjectId,
        QuotationId = order.QuotationId,
        OrderCode = order.OrderCode,
        CustomerId = order.CustomerId,
        SalesId = order.SalesId,
        OriginalTotalAmount = order.OriginalTotalAmount,
        ItemAdjustmentAmount = order.ItemAdjustmentAmount,
        AdditionalDiscountAmount = order.AdditionalDiscountAmount,
        FinalTotalAmount = order.FinalTotalAmount,
        DepositAmount = order.DepositAmount,
        PaidAmount = order.PaidAmount,
        RemainingAmount = order.RemainingAmount,
        Status = order.Status,
        AssignedSalesId = _salesId,
        AssignedDesignerId = Guid.NewGuid()
    };

    private ProjectDetailReadModel CreateProjectDetail() => new()
    {
        ProjectId = _projectId,
        CustomerId = _customerId,
        AssignedSalesId = _salesId,
        Status = ProjectStatus.ORDER_CONFIRMED
    };

    private static Payment CreateDepositPayment(PaymentStatus status, decimal paidAmount) => new()
    {
        PaymentId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        PaymentType = PaymentType.DEPOSIT,
        Amount = 30_000_000m,
        PaidAmount = paidAmount,
        RemainingAmount = 30_000_000m - paidAmount,
        Status = status
    };

    private sealed class TestOptions
    {
        public string Role { get; init; } = "SALES";

        public Order? Order { get; set; }

        public OrderDetailReadModel? OrderDetail { get; set; }

        public ProjectDetailReadModel? ProjectDetail { get; set; }

        public Payment? DepositPayment { get; init; }

        public TrackingPaymentRepository? PaymentRepository { get; set; }

        public TrackingUnitOfWork? UnitOfWork { get; set; }

        public OrderStatus OrderStatus { get; init; } = OrderStatus.DEPOSIT_PENDING;
    }

    private sealed class TrackingOrderRepository : IOrderRepository
    {
        private readonly Order _order;
        private OrderDetailReadModel _detail;

        public TrackingOrderRepository(Order order, OrderDetailReadModel detail)
        {
            _order = order;
            _detail = detail;
        }

        public Task<OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OrderDetailReadModel?>(
                orderId == _detail.OrderId ? _detail : null);
        }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Order?>(orderId == _order.OrderId ? _order : null);
        }

        public void Update(Order order)
        {
            _detail.AdditionalDiscountAmount = order.AdditionalDiscountAmount;
            _detail.DepositAmount = order.DepositAmount;
            _detail.FinalTotalAmount = order.FinalTotalAmount;
            _detail.PaidAmount = order.PaidAmount;
            _detail.RemainingAmount = order.RemainingAmount;
            _detail.Status = order.Status;
        }

        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);

        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IQueryable<Order> Query() => Enumerable.Empty<Order>().AsQueryable();

        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);

        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(Order entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class TrackingPaymentRepository : IPaymentRepository
    {
        private readonly Payment? _depositPayment;

        public TrackingPaymentRepository(Payment? depositPayment)
        {
            _depositPayment = depositPayment;
            if (_depositPayment is not null)
            {
                _depositPayment.PaymentType = PaymentType.DEPOSIT;
            }
        }

        public Task<Payment?> GetByOrderAndTypeAsync(
            Guid orderId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
        {
            if (_depositPayment is null || paymentType != PaymentType.DEPOSIT)
            {
                return Task.FromResult<Payment?>(null);
            }

            _depositPayment.OrderId = orderId;
            return Task.FromResult<Payment?>(_depositPayment);
        }

        public Task<decimal> SumOrderScopedPaidAmountAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_depositPayment?.PaidAmount ?? 0m);

        public void UpdatePayment(Payment payment)
        {
        }

        public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?> GetDetailAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(
            string paymentCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(
            string paymentCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentStatusByCodeReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentListItemReadModel>> GetListAsync(
            Infrastructure.ReadModels.Payments.PaymentQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentListItemReadModel>>([]);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentTransactionReadModel>>([]);

        public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ProviderTransactionExistsAsync(
            PaymentProvider provider,
            string providerTransactionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
            PaymentProvider provider,
            string providerReferenceCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransaction?>(null);

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Payment?> GetByProjectAndTypeAsync(
            Guid projectId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public void UpdateTransaction(PaymentTransaction transaction)
        {
        }
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProjectRepository(ProjectDetailReadModel? project, string role) : IProjectRepository
    {
        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(project?.ProjectId == projectId ? project : null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(role);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
