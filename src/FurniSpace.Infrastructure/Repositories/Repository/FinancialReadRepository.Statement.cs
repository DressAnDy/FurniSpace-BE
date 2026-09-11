#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed partial class FinancialReadRepository
{
    private const string StatementCredit = "CREDIT";
    private const string StatementDebit = "DEBIT";
    private const string StatementCollection = "COLLECTION";
    private const string StatementRefund = "REFUND";
    private const string StatementAdjustment = "ADJUSTMENT";

    public async Task<AdminFinancialProjectStatementReadModel?> GetProjectStatementAsync(
        AdminFinancialProjectStatementQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var project = await (
            from p in _dbContext.ProjectSet.AsNoTracking()
            where p.ProjectId == query.ProjectId
            join customer in _dbContext.AccountSet.AsNoTracking()
                on p.CustomerId equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            select new
            {
                p.ProjectId,
                p.ProjectCode,
                p.ProjectName,
                CustomerName = customer != null ? customer.FullName : null
            }).FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var allEntries = await BuildStatementEntriesAsync(query.ProjectId, cancellationToken);
        var openingBalance = allEntries
            .Where(e => e.OccurredAt.HasValue && e.OccurredAt.Value < query.FromUtc)
            .Sum(e => e.SignedAmount);

        var periodEntries = allEntries
            .Where(e =>
                e.OccurredAt.HasValue &&
                e.OccurredAt.Value >= query.FromUtc &&
                e.OccurredAt.Value < query.ToUtcExclusive)
            .ToList();

        var filtered = ApplyStatementFilters(periodEntries, query).ToList();
        var pageItems = PageStatementItems(filtered, openingBalance, query);

        return new AdminFinancialProjectStatementReadModel
        {
            ProjectId = project.ProjectId,
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            CustomerName = project.CustomerName,
            OpeningBalance = openingBalance,
            TotalCollected = periodEntries.Where(e => e.EntryType == StatementCollection).Sum(e => e.Amount),
            TotalRefunded = periodEntries.Where(e => e.EntryType == StatementRefund).Sum(e => e.Amount),
            NetCollected = periodEntries.Sum(e => e.SignedAmount),
            ClosingBalance = openingBalance + periodEntries.Sum(e => e.SignedAmount),
            Items = pageItems,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = filtered.Count
        };
    }

    private async Task<List<AdminFinancialProjectStatementItemReadModel>> BuildStatementEntriesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var entries = await LoadPaidPaymentStatementEntriesAsync(projectId, cancellationToken);
        entries.AddRange(await LoadAdjustmentStatementEntriesAsync(projectId, cancellationToken));
        entries.AddRange(await LoadRefundTransactionStatementEntriesAsync(projectId, cancellationToken));
        return entries;
    }

    private async Task<List<AdminFinancialProjectStatementItemReadModel>> LoadPaidPaymentStatementEntriesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var paidPayments = await (
            from payment in _dbContext.PaymentSet.AsNoTracking()
            where payment.ProjectId == projectId &&
                  payment.Status == PaymentStatus.PAID &&
                  payment.PaidAt.HasValue &&
                  payment.Currency == DefaultCurrency
            join order in _dbContext.OrderSet.AsNoTracking()
                on payment.OrderId equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new StatementPaymentRow(
                payment.PaymentId,
                payment.PaymentCode,
                payment.PaymentType,
                payment.Amount,
                payment.PaidAt,
                payment.OrderId,
                order != null ? order.OrderCode : null)).ToListAsync(cancellationToken);

        var providerByPayment = await LoadSuccessProvidersAsync(
            paidPayments.Select(p => p.PaymentId).ToList(),
            cancellationToken);

        var entries = new List<AdminFinancialProjectStatementItemReadModel>(paidPayments.Count);
        foreach (var payment in paidPayments)
        {
            providerByPayment.TryGetValue(payment.PaymentId, out var provider);
            entries.Add(ToPaidPaymentStatementEntry(payment, provider));
        }

        return entries;
    }

    private async Task<List<AdminFinancialProjectStatementItemReadModel>> LoadAdjustmentStatementEntriesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadSuccessfulTypedTransactionsAsync(
            projectId,
            PaymentTransactionType.ADJUSTMENT,
            excludePaidRefundPayments: false,
            cancellationToken);

        return rows.Select(ToAdjustmentStatementEntry).ToList();
    }

    private async Task<List<AdminFinancialProjectStatementItemReadModel>> LoadRefundTransactionStatementEntriesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadSuccessfulTypedTransactionsAsync(
            projectId,
            PaymentTransactionType.REFUND,
            excludePaidRefundPayments: true,
            cancellationToken);

        return rows.Select(ToRefundTransactionStatementEntry).ToList();
    }

    private async Task<List<StatementTxnRow>> LoadSuccessfulTypedTransactionsAsync(
        Guid projectId,
        PaymentTransactionType transactionType,
        bool excludePaidRefundPayments,
        CancellationToken cancellationToken)
    {
        var query =
            from txn in _dbContext.PaymentTransactionSet.AsNoTracking()
            where txn.ProjectId == projectId &&
                  txn.Status == PaymentTransactionStatus.SUCCESS &&
                  txn.TransactionType == transactionType &&
                  txn.Currency == DefaultCurrency
            join payment in _dbContext.PaymentSet.AsNoTracking()
                on txn.PaymentId equals payment.PaymentId into payments
            from payment in payments.DefaultIfEmpty()
            select new { txn, payment };

        var materialised = await query.ToListAsync(cancellationToken);
        if (excludePaidRefundPayments)
        {
            materialised = materialised
                .Where(row =>
                    row.payment is null ||
                    row.payment.PaymentType != PaymentType.REFUND ||
                    row.payment.Status != PaymentStatus.PAID)
                .ToList();
        }

        var orderIds = materialised
            .Select(row => row.txn.OrderId ?? row.payment?.OrderId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var orderCodes = orderIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _dbContext.OrderSet.AsNoTracking()
                .Where(order => orderIds.Contains(order.OrderId))
                .ToDictionaryAsync(order => order.OrderId, order => (string?)order.OrderCode, cancellationToken);

        return materialised.Select(row =>
        {
            var orderId = row.txn.OrderId ?? row.payment?.OrderId;
            orderCodes.TryGetValue(orderId ?? Guid.Empty, out var orderCode);
            return new StatementTxnRow(
                row.txn.PaymentTransactionId,
                row.txn.ConfirmedAt ?? row.txn.CreatedAt,
                row.txn.Amount,
                row.txn.PaymentProvider,
                row.txn.TransactionCode,
                row.payment?.PaymentId,
                row.payment?.PaymentCode,
                row.payment?.PaymentType,
                orderId,
                orderId.HasValue ? orderCode : null);
        }).ToList();
    }

    private async Task<Dictionary<Guid, PaymentProvider?>> LoadSuccessProvidersAsync(
        List<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return new Dictionary<Guid, PaymentProvider?>();
        }

        var providers = await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t =>
                paymentIds.Contains(t.PaymentId) &&
                t.Status == PaymentTransactionStatus.SUCCESS)
            .Select(t => new { t.PaymentId, t.PaymentProvider, SortAt = t.ConfirmedAt ?? t.CreatedAt })
            .ToListAsync(cancellationToken);

        return providers
            .GroupBy(t => t.PaymentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.SortAt).Select(t => t.PaymentProvider).FirstOrDefault());
    }

    private static List<AdminFinancialProjectStatementItemReadModel> PageStatementItems(
        List<AdminFinancialProjectStatementItemReadModel> filtered,
        decimal openingBalance,
        AdminFinancialProjectStatementQueryReadModel query)
    {
        var ascending = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = ascending
            ? filtered.OrderBy(e => e.OccurredAt).ThenBy(e => e.EntryId).ToList()
            : filtered.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.EntryId).ToList();

        var chronological = filtered
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.EntryId)
            .ToList();
        var running = openingBalance;
        var balanceByEntry = new Dictionary<Guid, decimal>(chronological.Count);
        foreach (var entry in chronological)
        {
            running += entry.SignedAmount;
            balanceByEntry[entry.EntryId] = running;
        }

        return ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(entry => entry with
            {
                RunningBalance = balanceByEntry.TryGetValue(entry.EntryId, out var bal) ? bal : openingBalance
            })
            .ToList();
    }

    private static AdminFinancialProjectStatementItemReadModel ToPaidPaymentStatementEntry(
        StatementPaymentRow payment,
        PaymentProvider? provider)
    {
        var isRefund = payment.PaymentType == PaymentType.REFUND;
        return new AdminFinancialProjectStatementItemReadModel
        {
            EntryId = payment.PaymentId,
            OccurredAt = payment.PaidAt,
            Direction = isRefund ? StatementDebit : StatementCredit,
            EntryType = isRefund ? StatementRefund : StatementCollection,
            PaymentType = payment.PaymentType?.ToString(),
            Description = BuildStatementDescription(payment.PaymentType, isRefund),
            ReferenceCode = payment.PaymentCode,
            OrderId = payment.OrderId,
            OrderCode = payment.OrderCode,
            PaymentId = payment.PaymentId,
            Provider = provider?.ToString(),
            Status = PaymentStatus.PAID.ToString(),
            Amount = payment.Amount,
            SignedAmount = isRefund ? -payment.Amount : payment.Amount
        };
    }

    private static AdminFinancialProjectStatementItemReadModel ToAdjustmentStatementEntry(StatementTxnRow txn) =>
        new()
        {
            EntryId = txn.PaymentTransactionId,
            OccurredAt = txn.OccurredAt,
            Direction = txn.Amount >= 0 ? StatementCredit : StatementDebit,
            EntryType = StatementAdjustment,
            PaymentType = txn.PaymentType?.ToString(),
            Description = "Payment adjustment",
            ReferenceCode = txn.TransactionCode ?? txn.PaymentCode,
            OrderId = txn.OrderId,
            OrderCode = txn.OrderCode,
            PaymentId = txn.PaymentId,
            Provider = txn.PaymentProvider?.ToString(),
            Status = PaymentTransactionStatus.SUCCESS.ToString(),
            Amount = Math.Abs(txn.Amount),
            SignedAmount = txn.Amount
        };

    private static AdminFinancialProjectStatementItemReadModel ToRefundTransactionStatementEntry(StatementTxnRow txn) =>
        new()
        {
            EntryId = txn.PaymentTransactionId,
            OccurredAt = txn.OccurredAt,
            Direction = StatementDebit,
            EntryType = StatementRefund,
            PaymentType = txn.PaymentType?.ToString() ?? nameof(PaymentType.REFUND),
            Description = "Refund issued",
            ReferenceCode = txn.TransactionCode ?? txn.PaymentCode,
            OrderId = txn.OrderId,
            OrderCode = txn.OrderCode,
            PaymentId = txn.PaymentId,
            Provider = txn.PaymentProvider?.ToString(),
            Status = PaymentTransactionStatus.SUCCESS.ToString(),
            Amount = Math.Abs(txn.Amount),
            SignedAmount = -Math.Abs(txn.Amount)
        };

    private static IEnumerable<AdminFinancialProjectStatementItemReadModel> ApplyStatementFilters(
        IEnumerable<AdminFinancialProjectStatementItemReadModel> entries,
        AdminFinancialProjectStatementQueryReadModel query)
    {
        if (!string.IsNullOrWhiteSpace(query.EntryType))
        {
            entries = entries.Where(e =>
                string.Equals(e.EntryType, query.EntryType, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PaymentType.HasValue)
        {
            var key = query.PaymentType.Value.ToString();
            entries = entries.Where(e => string.Equals(e.PaymentType, key, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            entries = entries.Where(e =>
                string.Equals(e.Status, query.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Provider.HasValue)
        {
            var key = query.Provider.Value.ToString();
            entries = entries.Where(e => string.Equals(e.Provider, key, StringComparison.OrdinalIgnoreCase));
        }

        return entries;
    }

    private static string BuildStatementDescription(PaymentType? paymentType, bool isRefund)
    {
        if (isRefund)
        {
            return "Refund issued";
        }

        return paymentType switch
        {
            PaymentType.PROJECT_START_FEE => "Project start fee collected",
            PaymentType.DEPOSIT => "Deposit collected",
            PaymentType.REMAINING_PAYMENT => "Remaining payment collected",
            PaymentType.FULL_PAYMENT => "Full payment collected",
            _ => "Payment collected"
        };
    }

    private sealed record StatementPaymentRow(
        Guid PaymentId,
        string? PaymentCode,
        PaymentType? PaymentType,
        decimal Amount,
        DateTime? PaidAt,
        Guid? OrderId,
        string? OrderCode);

    private sealed record StatementTxnRow(
        Guid PaymentTransactionId,
        DateTime? OccurredAt,
        decimal Amount,
        PaymentProvider? PaymentProvider,
        string? TransactionCode,
        Guid? PaymentId,
        string? PaymentCode,
        PaymentType? PaymentType,
        Guid? OrderId,
        string? OrderCode);
}
