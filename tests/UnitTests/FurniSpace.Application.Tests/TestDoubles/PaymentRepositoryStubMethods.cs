#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Payments;

namespace FurniSpace.Application.Tests.TestDoubles;

internal static class PaymentRepositoryStubMethods
{
    internal static Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    internal static Task<PaymentSummaryReadModel> GetSummaryAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentSummaryReadModel());

    internal static Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Payment>>([]);

    internal static Task<PaymentTransaction?> GetTransactionByIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    internal static Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
        Guid paymentId,
        PaymentProvider provider,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransactionReadModel?>(null);

    internal static Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransactionReadModel?>(null);

    internal static Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
