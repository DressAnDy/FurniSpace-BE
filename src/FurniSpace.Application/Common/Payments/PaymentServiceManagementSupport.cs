using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Payments;
using Mapster;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentServiceManagementSupport
{
    internal const int MaxPageSize = 100;

    internal static string? ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return "Page size must be between 1 and 100.";
        }

        return null;
    }

    internal static PaymentQueryReadModel BuildScopedQuery(
        PaymentQueryDto query,
        string? role,
        Guid currentUserId)
    {
        var readQuery = query.Adapt<PaymentQueryReadModel>();
        readQuery.AccessRole = role;
        readQuery.AccessUserId = currentUserId;
        return readQuery;
    }

    internal static PaymentQueryReadModel BuildScopeOnly(string? role, Guid currentUserId)
    {
        return new PaymentQueryReadModel
        {
            AccessRole = role,
            AccessUserId = currentUserId
        };
    }

    internal static bool IsCustomerOwner(PaymentDetailReadModel detail, Guid currentUserId)
    {
        return detail.PaidBy == currentUserId;
    }

    internal static bool IsValidHttpsUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
            Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    internal static PaymentListItemDto ToListItemDto(
        PaymentListItemReadModel item,
        bool hasSuccessfulTransaction,
        DateTime utcNow)
    {
        return new PaymentListItemDto
        {
            PaymentId = item.PaymentId,
            PaymentCode = item.PaymentCode,
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            PaymentType = item.PaymentType,
            Amount = item.Amount,
            Currency = item.Currency,
            Status = item.Status,
            ExpiredAt = item.ExpiredAt,
            PaidAt = item.PaidAt,
            CreatedAt = item.CreatedAt,
            IsPayable = PaymentPayableEvaluator.IsPayable(
                item.Status,
                item.Amount,
                item.ExpiredAt,
                hasSuccessfulTransaction,
                utcNow)
        };
    }

    internal static PaymentLatestTransactionDto? ToLatestTransactionDto(PaymentTransactionReadModel? transaction)
    {
        if (transaction is null)
        {
            return null;
        }

        return new PaymentLatestTransactionDto
        {
            PaymentTransactionId = transaction.PaymentTransactionId,
            TransactionCode = transaction.TransactionCode,
            PaymentProvider = transaction.PaymentProvider,
            PaymentMethod = transaction.PaymentMethod,
            Status = transaction.Status,
            PaymentUrl = transaction.PaymentUrl,
            QrContent = transaction.QrContent,
            CreatedAt = transaction.CreatedAt
        };
    }

    internal static PaymentTransactionAttemptResponseDto ToAttemptResponse(
        PaymentTransactionReadModel transaction,
        Payment payment)
    {
        return new PaymentTransactionAttemptResponseDto
        {
            PaymentTransactionId = transaction.PaymentTransactionId,
            PaymentId = transaction.PaymentId,
            TransactionCode = transaction.TransactionCode,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            PaymentProvider = transaction.PaymentProvider,
            PaymentMethod = transaction.PaymentMethod,
            PaymentUrl = transaction.PaymentUrl,
            QrContent = transaction.QrContent,
            PaymentStatus = payment.Status
        };
    }
}
