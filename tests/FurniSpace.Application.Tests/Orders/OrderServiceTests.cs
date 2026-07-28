#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderServiceTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();

    [Fact]
    public async Task GetByProjectAsync_WithEmptyUser_ReturnsUnauthorized()
    {
        var service = BuildService(options: null);

        var result = await service.GetByProjectAsync(_projectId, Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenProjectMissing_ReturnsProjectNotFound()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "ADMIN" });

        var result = await service.GetByProjectAsync(_projectId, Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_CustomerSeesOnlyAccessibleOrders()
    {
        var ownOrder = CreateListItem(OrderStatus.DEPOSIT_PENDING, _customerId);
        var otherOrder = CreateListItem(OrderStatus.DEPOSIT_PENDING, Guid.NewGuid());
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Orders = [ownOrder, otherOrder]
        });

        var result = await service.GetByProjectAsync(_projectId, _customerId);

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ownOrder.OrderId, item.OrderId);
    }

    [Fact]
    public async Task GetByProjectAsync_ProductionSeesProductionReadyOrdersOnly()
    {
        var visible = CreateListItem(OrderStatus.DEPOSIT_PAID, _customerId);
        var hidden = CreateListItem(OrderStatus.DEPOSIT_PENDING, _customerId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            ProjectDetail = CreateProjectDetail(),
            Orders = [visible, hidden]
        });

        var result = await service.GetByProjectAsync(_projectId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(visible.OrderId, item.OrderId);
        Assert.Equal(OrderStatus.DEPOSIT_PAID, item.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WhenOrderMissing_ReturnsOrderNotFound()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "ADMIN", ProjectDetail = CreateProjectDetail() });

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WhenForbidden_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            OrderDetail = CreateDetail(orderId, Guid.NewGuid())
        });

        var result = await service.GetDetailAsync(orderId, _customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WhenAuthorized_ReturnsMappedDetail()
    {
        var orderId = Guid.NewGuid();
        var detail = CreateDetail(orderId, _customerId);
        detail.Items =
        [
            new OrderItemDetailReadModel
            {
                OrderItemId = Guid.NewGuid(),
                ItemName = "Counter",
                Quantity = 1,
                Status = OrderItemStatus.READY,
                DeliveredQuantity = 1,
                CustomerConfirmedAt = null,
                UnitPrice = 100m,
                SubtotalAmount = 100m
            }
        ];
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            OrderDetail = detail
        });

        var result = await service.GetDetailAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(orderId, result.Data!.OrderId);
        Assert.Equal("ORD-001", result.Data.OrderCode);
        Assert.Single(result.Data.Items);
        Assert.Equal("Counter", result.Data.Items[0].ItemName);
        Assert.Equal(OrderItemStatus.READY, result.Data.Items[0].Status);
        Assert.Equal(1, result.Data.Items[0].DeliveredQuantity);
        Assert.Null(result.Data.Items[0].CustomerConfirmedAt);
    }

    private static OrderService BuildService(OrderServiceTestOptions? options = null)
    {
        options ??= new OrderServiceTestOptions();
        return new OrderService(
            new FakeOrderRepository(options.Orders, options.OrderDetail),
            new FakeProjectRepository(options.ProjectDetail, options.Role),
            new EmptyPaymentRepository(),
            new EmptyProjectScheduleRepository(),
            new FakeUnitOfWork());
    }

    private ProjectDetailReadModel CreateProjectDetail()
    {
        return new ProjectDetailReadModel
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
    }

    private OrderListItemReadModel CreateListItem(OrderStatus status, Guid customerId)
    {
        return new OrderListItemReadModel
        {
            OrderId = Guid.NewGuid(),
            ProjectId = _projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-LIST",
            OriginalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CustomerId = customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId
        };
    }

    private OrderDetailReadModel CreateDetail(Guid orderId, Guid customerId)
    {
        return new OrderDetailReadModel
        {
            OrderId = orderId,
            ProjectId = _projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = customerId,
            SalesId = _salesId,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId
        };
    }

    private sealed class OrderServiceTestOptions
    {
        public string Role { get; init; } = "ADMIN";

        public ProjectDetailReadModel? ProjectDetail { get; init; }

        public IReadOnlyList<OrderListItemReadModel> Orders { get; init; } = [];

        public OrderDetailReadModel? OrderDetail { get; init; }
    }

    private sealed class FakeOrderRepository(
        IReadOnlyList<OrderListItemReadModel> orders,
        OrderDetailReadModel? orderDetail) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var items = orders.Where(order => order.ProjectId == projectId).ToList();
            return Task.FromResult<IReadOnlyList<OrderListItemReadModel>>(items);
        }

        public Task<OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(orderDetail?.OrderId == orderId ? orderDetail : null);
        }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<Order?>(null);

        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Order order)
        {
        }

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

    private sealed class FakeProjectRepository(ProjectDetailReadModel? project, string role) : IProjectRepository
    {
        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(project?.ProjectId == projectId ? project : null);
        }

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

    private sealed class EmptyPaymentRepository : IPaymentRepository
    {
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

        public Task<Payment?> GetByOrderAndTypeAsync(
            Guid orderId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<Payment?> GetByProjectAndTypeAsync(
            Guid projectId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<decimal> SumOrderScopedPaidAmountAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<bool> HasSuccessfulTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.CountAsync(query, cancellationToken);

        public Task<PaymentSummaryReadModel> GetSummaryAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetSummaryAsync(query, utcNow, cancellationToken);

        public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetExpiredPaymentsForSyncAsync(query, utcNow, cancellationToken);

        public Task<PaymentTransaction?> GetTransactionByIdAsync(
            Guid paymentTransactionId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetTransactionByIdAsync(paymentTransactionId, cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
            Guid paymentId,
            PaymentProvider provider,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestPendingTransactionAsync(
                paymentId,
                provider,
                method,
                cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestTransactionAsync(paymentId, cancellationToken);

        public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
            IReadOnlyCollection<Guid> paymentIds,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetPaymentIdsWithSuccessfulTransactionAsync(paymentIds, cancellationToken);

        public void UpdatePayment(Payment payment)
        {
        }

        public void UpdateTransaction(PaymentTransaction transaction)
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

}
