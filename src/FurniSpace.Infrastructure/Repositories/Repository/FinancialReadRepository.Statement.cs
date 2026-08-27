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

        var totalCollected = periodEntries
            .Where(e => e.EntryType == StatementCollection)
            .Sum(e => e.Amount);
        var totalRefunded = periodEntries
            .Where(e => e.EntryType == StatementRefund)
            .Sum(e => e.Amount);
        var netCollected = periodEntries.Sum(e => e.SignedAmount);
        var closingBalance = openingBalance + netCollected;

        var filtered = ApplyStatementFilters(periodEntries, query).ToList();
        var ascending = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = ascending
            ? filtered.OrderBy(e => e.OccurredAt).ThenBy(e => e.EntryId).ToList()
            : filtered.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.EntryId).ToList();

        // Running balance is always chronological from opening.
        var chronological = filtered
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.EntryId)
            .ToList();
        var running = openingBalance;
        var balanceByEntry = new Dictionary<Guid, decimal>();
        foreach (var entry in chronological)
        {
            running += entry.SignedAmount;
            balanceByEntry[entry.EntryId] = running;
        }

        var pageItems = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(entry => entry with
            {
                RunningBalance = balanceByEntry.TryGetValue(entry.EntryId, out var bal) ? bal : openingBalance
            })
            .ToList();

        return new AdminFinancialProjectStatementReadModel
        {
            ProjectId = project.ProjectId,
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            CustomerName = project.CustomerName,
            OpeningBalance = openingBalance,
            TotalCollected = totalCollected,
            TotalRefunded = totalRefunded,
            NetCollected = netCollected,
            ClosingBalance = closingBalance,
            Items = pageItems,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = ordered.Count
        };
    }

    private async Task<List<AdminFinancialProjectStatementItemReadModel>> BuildStatementEntriesAsync(
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
            select new
            {
                payment.PaymentId,
                payment.PaymentCode,
                payment.PaymentType,
                payment.Amount,
                payment.PaidAt,
                payment.OrderId,
                OrderCode = order != null ? order.OrderCode : null
            }).ToListAsync(cancellationToken);

        var paymentIds = paidPayments.Select(p => p.PaymentId).ToList();
        var providers = paymentIds.Count == 0
            ? []
            : await _dbContext.PaymentTransactionSet.AsNoTracking()
                .Where(t =>
                    paymentIds.Contains(t.PaymentId) &&
                    t.Status == PaymentTransactionStatus.SUCCESS)
                .Select(t => new { t.PaymentId, t.PaymentProvider, SortAt = t.ConfirmedAt ?? t.CreatedAt })
                .ToListAsync(cancellationToken);
        var providerByPayment = providers
            .GroupBy(t => t.PaymentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.SortAt).Select(t => t.PaymentProvider).FirstOrDefault());

        var entries = new List<AdminFinancialProjectStatementItemReadModel>();
        foreach (var payment in paidPayments)
        {
            providerByPayment.TryGetValue(payment.PaymentId, out var provider);
            var isRefund = payment.PaymentType == PaymentType.REFUND;
            entries.Add(new AdminFinancialProjectStatementItemReadModel
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
            });
        }

        var adjustmentTxns = await (
            from txn in _dbContext.PaymentTransactionSet.AsNoTracking()
            where txn.ProjectId == projectId &&
                  txn.Status == PaymentTransactionStatus.SUCCESS &&
                  txn.TransactionType == PaymentTransactionType.ADJUSTMENT &&
                  txn.Currency == DefaultCurrency
            join payment in _dbContext.PaymentSet.AsNoTracking()
                on txn.PaymentId equals payment.PaymentId into payments
            from payment in payments.DefaultIfEmpty()
            join order in _dbContext.OrderSet.AsNoTracking()
                on (txn.OrderId ?? payment.OrderId) equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new
            {
                txn.PaymentTransactionId,
                OccurredAt = txn.ConfirmedAt ?? txn.CreatedAt,
                txn.Amount,
                txn.PaymentProvider,
                txn.TransactionCode,
                PaymentId = (Guid?)payment.PaymentId,
                PaymentCode = payment != null ? payment.PaymentCode : null,
                PaymentType = payment != null ? payment.PaymentType : null,
                OrderId = txn.OrderId ?? (payment != null ? payment.OrderId : null),
                OrderCode = order != null ? order.OrderCode : null
            }).ToListAsync(cancellationToken);

        foreach (var txn in adjustmentTxns)
        {
            entries.Add(new AdminFinancialProjectStatementItemReadModel
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
            });
        }

        // SUCCESS refund transactions that are not already covered by PaymentType.REFUND PAID rows.
        var refundTxns = await (
            from txn in _dbContext.PaymentTransactionSet.AsNoTracking()
            where txn.ProjectId == projectId &&
                  txn.Status == PaymentTransactionStatus.SUCCESS &&
                  txn.TransactionType == PaymentTransactionType.REFUND &&
                  txn.Currency == DefaultCurrency
            join payment in _dbContext.PaymentSet.AsNoTracking()
                on txn.PaymentId equals payment.PaymentId into payments
            from payment in payments.DefaultIfEmpty()
            where payment == null || payment.PaymentType != PaymentType.REFUND || payment.Status != PaymentStatus.PAID
            join order in _dbContext.OrderSet.AsNoTracking()
                on (txn.OrderId ?? (payment != null ? payment.OrderId : null)) equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new
            {
                txn.PaymentTransactionId,
                OccurredAt = txn.ConfirmedAt ?? txn.CreatedAt,
                txn.Amount,
                txn.PaymentProvider,
                txn.TransactionCode,
                PaymentId = (Guid?)payment.PaymentId,
                PaymentCode = payment != null ? payment.PaymentCode : null,
                PaymentType = payment != null ? payment.PaymentType : (PaymentType?)PaymentType.REFUND,
                OrderId = txn.OrderId ?? (payment != null ? payment.OrderId : null),
                OrderCode = order != null ? order.OrderCode : null
            }).ToListAsync(cancellationToken);

        foreach (var txn in refundTxns)
        {
            entries.Add(new AdminFinancialProjectStatementItemReadModel
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
            });
        }

        return entries;
    }

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
}
