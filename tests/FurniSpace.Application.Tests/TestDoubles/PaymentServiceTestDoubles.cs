#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Tests.TestDoubles;

internal sealed class PaymentServiceFakeRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, Payment> _payments = [];
    private readonly Dictionary<Guid, PaymentDetailReadModel> _details = [];
    private readonly List<PaymentTransaction> _transactions = [];
    private readonly List<PaymentListItemReadModel> _listItems = [];

    public List<Payment> NewPayments { get; } = [];

    public int SaveChangesCallCount { get; set; }

    public void SeedPayment(Payment payment, PaymentDetailReadModel? detail = null)
    {
        _payments[payment.PaymentId] = payment;
        _details[payment.PaymentId] = detail ?? CreateDetailFromPayment(payment);
    }

    public void SeedListItem(PaymentListItemReadModel item)
    {
        _listItems.Add(item);
    }

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        _payments.TryGetValue(paymentId, out var payment);
        return Task.FromResult(payment);
    }

    public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        _details.TryGetValue(paymentId, out var detail);
        return Task.FromResult(detail);
    }

    public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(
        string paymentCode,
        CancellationToken cancellationToken = default)
    {
        var detail = _details.Values.FirstOrDefault(item => item.PaymentCode == paymentCode);
        return Task.FromResult(detail);
    }

    public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(
        string paymentCode,
        CancellationToken cancellationToken = default)
    {
        if (!_details.Values.TryFirst(item => item.PaymentCode == paymentCode, out var detail))
        {
            return Task.FromResult<PaymentStatusByCodeReadModel?>(null);
        }

        return Task.FromResult<PaymentStatusByCodeReadModel?>(new PaymentStatusByCodeReadModel
        {
            PaymentId = detail!.PaymentId,
            PaymentCode = detail.PaymentCode,
            Status = detail.Status,
            Amount = detail.Amount,
            PaidAt = detail.PaidAt
        });
    }

    public Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(
        PaymentQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var items = _listItems.AsEnumerable();
        if (query.ProjectId.HasValue)
        {
            items = items.Where(item => item.ProjectId == query.ProjectId.Value);
        }

        if (query.OrderId.HasValue)
        {
            items = items.Where(item => item.OrderId == query.OrderId.Value);
        }

        items = items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        return Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>(items.ToList());
    }

    public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
    {
        var count = _listItems.Count;
        if (query.ProjectId.HasValue)
        {
            count = _listItems.Count(item => item.ProjectId == query.ProjectId.Value);
        }

        if (query.OrderId.HasValue)
        {
            count = _listItems.Count(item => item.OrderId == query.OrderId.Value);
        }

        return Task.FromResult(count);
    }

    public Task<PaymentSummaryReadModel> GetSummaryAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentSummaryReadModel());
    }

    public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Payment>>([]);
    }

    public Task<PaymentTransaction?> GetTransactionByIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_transactions.FirstOrDefault(
            transaction => transaction.PaymentTransactionId == paymentTransactionId));
    }

    public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
        Guid paymentId,
        PaymentProvider provider,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
    {
        var transaction = _transactions
            .Where(item =>
                item.PaymentId == paymentId &&
                item.Status == PaymentTransactionStatus.PENDING &&
                item.PaymentProvider == provider &&
                item.PaymentMethod == method)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(transaction is null ? null : MapTransaction(transaction));
    }

    public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var transaction = _transactions
            .Where(item => item.PaymentId == paymentId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(transaction is null ? null : MapTransaction(transaction));
    }

    public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = _transactions
            .Where(transaction =>
                paymentIds.Contains(transaction.PaymentId) &&
                transaction.Status == PaymentTransactionStatus.SUCCESS)
            .Select(transaction => transaction.PaymentId)
            .Distinct()
            .ToHashSet();

        return Task.FromResult<IReadOnlySet<Guid>>(ids);
    }

    public Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var items = _transactions
            .Where(transaction => transaction.PaymentId == paymentId)
            .Select(transaction => new PaymentTransactionReadModel
            {
                PaymentTransactionId = transaction.PaymentTransactionId,
                PaymentId = transaction.PaymentId,
                TransactionCode = transaction.TransactionCode,
                TransactionType = transaction.TransactionType,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                PaymentProvider = transaction.PaymentProvider,
                PaymentMethod = transaction.PaymentMethod,
                ProviderTransactionId = transaction.ProviderTransactionId,
                ProviderReferenceCode = transaction.ProviderReferenceCode,
                Status = transaction.Status,
                PaymentUrl = transaction.PaymentUrl,
                QrContent = transaction.QrContent,
                FailureReason = transaction.FailureReason,
                TransactionTime = transaction.TransactionTime,
                CreatedAt = transaction.CreatedAt
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>(items);
    }

    private static PaymentTransactionReadModel MapTransaction(PaymentTransaction transaction)
    {
        return new PaymentTransactionReadModel
        {
            PaymentTransactionId = transaction.PaymentTransactionId,
            PaymentId = transaction.PaymentId,
            TransactionCode = transaction.TransactionCode,
            TransactionType = transaction.TransactionType,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            PaymentProvider = transaction.PaymentProvider,
            PaymentMethod = transaction.PaymentMethod,
            ProviderTransactionId = transaction.ProviderTransactionId,
            ProviderReferenceCode = transaction.ProviderReferenceCode,
            Status = transaction.Status,
            PaymentUrl = transaction.PaymentUrl,
            QrContent = transaction.QrContent,
            FailureReason = transaction.FailureReason,
            TransactionTime = transaction.TransactionTime,
            CreatedAt = transaction.CreatedAt
        };
    }

    public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_payments.Values.Any(payment => payment.PaymentCode == paymentCode));
    }

    public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_transactions.Any(transaction => transaction.TransactionCode == transactionCode));
    }

    public Task<bool> ProviderTransactionExistsAsync(
        PaymentProvider provider,
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_transactions.Any(
            transaction =>
                transaction.PaymentProvider == PaymentProvider.PAYOS &&
                transaction.ProviderReferenceCode == orderCode));
    }

    public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
        PaymentProvider provider,
        string providerReferenceCode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_transactions.FirstOrDefault(
            transaction =>
                transaction.PaymentProvider == provider &&
                transaction.ProviderReferenceCode == providerReferenceCode));
    }

    public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _payments[payment.PaymentId] = payment;
        _details[payment.PaymentId] = CreateDetailFromPayment(payment);
        NewPayments.Add(payment);
        return Task.CompletedTask;
    }

    public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        _transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public void UpdatePayment(Payment payment)
    {
        _payments[payment.PaymentId] = payment;
        if (_details.TryGetValue(payment.PaymentId, out var detail))
        {
            detail.Status = payment.Status;
            detail.PaidAt = payment.PaidAt;
        }
    }

    public void UpdateTransaction(PaymentTransaction transaction)
    {
        var index = _transactions.FindIndex(
            item => item.PaymentTransactionId == transaction.PaymentTransactionId);
        if (index >= 0)
        {
            _transactions[index] = transaction;
        }
    }

    public Task<Payment?> GetByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        var payment = _payments.Values
            .Where(item => item.OrderId == orderId && item.PaymentType == paymentType)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByProjectAndTypeAsync(
        Guid projectId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        var payment = _payments.Values
            .Where(item => item.ProjectId == projectId && item.PaymentType == paymentType)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(payment);
    }

    public Task<decimal> SumOrderScopedPaidAmountAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var sum = _payments.Values
            .Where(payment =>
                payment.OrderId == orderId &&
                payment.PaymentType is PaymentType.DEPOSIT or PaymentType.REMAINING_PAYMENT or PaymentType.FULL_PAYMENT &&
                payment.Status == PaymentStatus.PAID)
            .Sum(payment => payment.Amount);
        return Task.FromResult(sum);
    }

    public Task<bool> HasSuccessfulTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var hasSuccess = _transactions.Any(
            transaction =>
                transaction.PaymentId == paymentId &&
                transaction.Status == PaymentTransactionStatus.SUCCESS);
        return Task.FromResult(hasSuccess);
    }

    private static PaymentDetailReadModel CreateDetailFromPayment(Payment payment)
    {
        return new PaymentDetailReadModel
        {
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            OrderId = payment.OrderId,
            PaymentCode = payment.PaymentCode,
            PaymentType = payment.PaymentType,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            ExpiredAt = payment.ExpiredAt,
            PaidAt = payment.PaidAt,
            CustomerId = payment.PaidBy ?? Guid.Empty
        };
    }
}

internal sealed class PaymentServiceFakeProjectRepository : IProjectRepository
{
    public ProjectDetailReadModel? ProjectDetail { get; set; }
    public string Role { get; set; } = "ADMIN";
    public Project? ProjectEntity { get; set; }

    public Task<ProjectDetailReadModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProjectDetail?.ProjectId == projectId ? ProjectDetail : null);
    }

    public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(Role);
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProjectEntity?.ProjectId == id ? ProjectEntity : null);
    }

    public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Project>>([]);

    public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Update(Project entity) => ProjectEntity = entity;

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

internal sealed class PaymentServiceFakeOrderRepository : IOrderRepository
{
    public OrderDetailReadModel? OrderDetail { get; set; }
    public Order? OrderEntity { get; set; }

    public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);

    public Task<OrderDetailReadModel?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OrderDetail?.OrderId == orderId ? OrderDetail : null);
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OrderEntity?.OrderId == orderId ? OrderEntity : null);
    }

    public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task AddAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Update(Order order) => OrderEntity = order;

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

internal sealed class PaymentServiceFakePayOsClient : IPayOsClient
{
    public bool ShouldFail { get; set; }
    public PayOsCreatePaymentLinkResult Result { get; set; } = new()
    {
        CheckoutUrl = "https://pay.payos.vn/checkout",
        QrCode = "qr-data",
        PaymentLinkId = "plink-001"
    };

    public Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
        PayOsCreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("PayOS unavailable.");
        }

        return Task.FromResult(Result);
    }

    public Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PayOsVerifiedWebhookData());
    }

    public Task<string> ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(webhookUrl);
    }
}

internal static class EnumerableExtensions
{
    public static bool TryFirst<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate,
        out T? value)
    {
        foreach (var item in source)
        {
            if (predicate(item))
            {
                value = item;
                return true;
            }
        }

        value = default;
        return false;
    }
}
