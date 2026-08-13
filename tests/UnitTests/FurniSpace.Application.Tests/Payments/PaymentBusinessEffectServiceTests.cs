#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentBusinessEffectServiceTests
{
    [Fact]
    public async Task ApplyAsync_DepositPaid_UpdatesOrderStatusAndDispatchesNotification()
    {
        var orderId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var order = CreateOrder(orderId, salesId, OrderStatus.DEPOSIT_PENDING);
        var orders = new FakeOrderRepository { Order = order };
        var dispatcher = new FakeNotificationDispatcher();
        var service = new PaymentBusinessEffectService(
            new FakePaymentRepository(),
            orders,
            new FakeProjectRepository(),
            dispatcher);

        var payment = CreatePayment(orderId, PaymentType.DEPOSIT, PaymentStatus.PAID);
        payment.PaidBy = order.CustomerId;

        await service.ApplyAsync(payment);

        Assert.Equal(OrderStatus.DEPOSIT_PAID, orders.Order!.Status);
        Assert.Single(dispatcher.Dispatched);
        Assert.Equal(NotificationType.PaymentPaid, dispatcher.Dispatched[0].Type);
    }

    [Fact]
    public async Task ApplyAsync_RemainingPaid_DoesNotAutoCompleteOrder()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(orderId, Guid.NewGuid(), OrderStatus.FINAL_PAYMENT_PENDING);
        order.RemainingAmount = 0m;
        order.CustomerConfirmedDeliveryAt = DateTime.UtcNow;
        var projectId = order.ProjectId;
        var orders = new FakeOrderRepository { Order = order };
        var projects = new FakeProjectRepository
        {
            Project = new Project
            {
                ProjectId = projectId,
                Status = ProjectStatus.ORDER_CONFIRMED
            }
        };
        var payments = new FakePaymentRepository { SummedPaidAmount = 100m };
        var service = new PaymentBusinessEffectService(
            payments,
            orders,
            projects);

        var payment = CreatePayment(orderId, PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID);

        await service.ApplyAsync(payment);

        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, orders.Order!.Status);
        Assert.Equal(ProjectStatus.ORDER_CONFIRMED, projects.Project!.Status);
    }

    [Fact]
    public async Task ApplyAsync_ProjectStartFeePaid_UpdatesProjectStatus()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projects = new FakeProjectRepository
        {
            Project = new Project
            {
                ProjectId = projectId,
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = salesId,
                ProjectName = "Cafe",
                Status = ProjectStatus.IN_CONSULTATION
            }
        };
        var dispatcher = new FakeNotificationDispatcher();
        var customerId = Guid.NewGuid();
        var service = new PaymentBusinessEffectService(
            new FakePaymentRepository(),
            new FakeOrderRepository(),
            projects,
            dispatcher);

        var payment = CreatePayment(null, PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, projectId);
        payment.PaidBy = customerId;

        await service.ApplyAsync(payment);

        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, projects.Project!.Status);
        Assert.Contains(dispatcher.Dispatched, item => item.Type == NotificationType.PaymentPaid);
        Assert.Contains(dispatcher.Dispatched, item => item.Type == NotificationType.ProjectStatusChanged);
    }

    [Fact]
    public async Task ApplyAsync_ProjectStartFeePaid_WhenNotificationFails_StillUpdatesProject()
    {
        var projectId = Guid.NewGuid();
        var projects = new FakeProjectRepository
        {
            Project = new Project
            {
                ProjectId = projectId,
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = Guid.NewGuid(),
                Status = ProjectStatus.IN_CONSULTATION,
                ProjectName = "Cafe"
            }
        };
        var service = new PaymentBusinessEffectService(
            new FakePaymentRepository(),
            new FakeOrderRepository(),
            projects,
            new ThrowingNotificationDispatcher());

        var payment = CreatePayment(null, PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, projectId);

        await service.ApplyAsync(payment);

        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, projects.Project!.Status);
    }

    [Fact]
    public async Task ApplyAsync_PaymentPaid_IncludesAssignedSalesFromStakeholders()
    {
        var orderId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var service = new PaymentBusinessEffectService(
            new FakePaymentRepository(),
            new FakeOrderRepository { Order = CreateOrder(orderId, salesId, OrderStatus.DEPOSIT_PENDING) },
            new FakeProjectRepository(),
            dispatcher,
            new StubStakeholderResolver(projectId, salesId));

        var payment = CreatePayment(orderId, PaymentType.DEPOSIT, PaymentStatus.PAID, projectId);
        payment.PaidBy = customerId;

        await service.ApplyAsync(payment);

        var paid = Assert.Single(dispatcher.Dispatched, item => item.Type == NotificationType.PaymentPaid);
        Assert.Contains(customerId, paid.Receivers);
        Assert.Contains(salesId, paid.Receivers);
    }

    [Fact]
    public async Task ApplyAsync_OrderScopedPayment_RecalculatesOrderPaidAmount()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(orderId, Guid.NewGuid(), OrderStatus.DEPOSIT_PENDING);
        order.FinalTotalAmount = 100m;
        var orders = new FakeOrderRepository { Order = order };
        var payments = new FakePaymentRepository { SummedPaidAmount = 30m };
        var service = new PaymentBusinessEffectService(
            payments,
            orders,
            new FakeProjectRepository());

        var payment = CreatePayment(orderId, PaymentType.DEPOSIT, PaymentStatus.PROCESSING);

        await service.ApplyAsync(payment);

        Assert.Equal(30m, orders.Order!.PaidAmount);
        Assert.Equal(70m, orders.Order.RemainingAmount);
    }

    [Fact]
    public async Task ApplyAsync_WhenPaymentNotPaid_DoesNotApplyTypeSpecificEffects()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(orderId, Guid.NewGuid(), OrderStatus.DEPOSIT_PENDING);
        var orders = new FakeOrderRepository { Order = order };
        var service = new PaymentBusinessEffectService(
            new FakePaymentRepository(),
            orders,
            new FakeProjectRepository());

        var payment = CreatePayment(orderId, PaymentType.DEPOSIT, PaymentStatus.PENDING);

        await service.ApplyAsync(payment);

        Assert.Equal(OrderStatus.DEPOSIT_PENDING, orders.Order!.Status);
    }

    private static Order CreateOrder(Guid orderId, Guid salesId, OrderStatus status)
    {
        return new Order
        {
            OrderId = orderId,
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = Guid.NewGuid(),
            SalesId = salesId,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = status
        };
    }

    private static Payment CreatePayment(
        Guid? orderId,
        PaymentType paymentType,
        PaymentStatus status,
        Guid? projectId = null)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = projectId ?? Guid.NewGuid(),
            OrderId = orderId,
            PaymentCode = "FS12345678",
            PaymentType = paymentType,
            Amount = 100m,
            Status = status
        };
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public decimal SummedPaidAmount { get; set; }

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
            => Task.FromResult(SummedPaidAmount);

        public Task<bool> HasSuccessfulTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public Order? Order { get; set; }

        public Task<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>>([]);

        public Task<Infrastructure.ReadModels.Orders.OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Orders.OrderDetailReadModel?>(null);

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Order?.OrderId == orderId ? Order : null);

        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Update(Order order) => Order = order;

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

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public Project? Project { get; set; }

        public Task<Infrastructure.ReadModels.Projects.ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.ProjectDetailReadModel?>(null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Project?.ProjectId == id ? Project : null);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Project entity) => Project = entity;

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

        public Task<Infrastructure.ReadModels.Projects.DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectListItemReadModel>> GetListAsync(
            Infrastructure.ReadModels.Projects.ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(
            Infrastructure.ReadModels.Projects.ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel>>([]);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectByUserItemReadModel>> GetByUserAsync(
            Infrastructure.ReadModels.Projects.ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(
            Infrastructure.ReadModels.Projects.ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public List<(NotificationType Type, IReadOnlyDictionary<string, string> Data, IReadOnlyList<Guid> Receivers)> Dispatched { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            Dispatched.Add((type, parameters, receiverIds.ToList()));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            throw new InvalidOperationException("Notification failed.");
        }
    }

    private sealed class StubStakeholderResolver(Guid projectId, Guid salesId) : IProjectStakeholderResolver
    {
        public Task<ProjectStakeholders?> ResolveAsync(
            Guid requestedProjectId,
            CancellationToken cancellationToken = default)
        {
            if (requestedProjectId != projectId)
            {
                return Task.FromResult<ProjectStakeholders?>(null);
            }

            return Task.FromResult<ProjectStakeholders?>(new ProjectStakeholders
            {
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = salesId
            });
        }
    }
}
