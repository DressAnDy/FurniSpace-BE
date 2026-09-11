using FurniSpace.Infrastructure.ReadModels.Financial;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IFinancialDiscountReadRepository
{
    Task<AdminFinancialDiscountSummaryReadModel> GetSummaryAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>> GetOrderMetricsAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountOrderMetricsAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<AdminFinancialDiscountOrderMetricsReadModel?> GetOrderMetricsByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialDiscountOrderItemReadModel>> GetOrderItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel>> GetTrendAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialDiscountExceptionReadModel>> GetExceptionsAsync(
        AdminFinancialDiscountQueryReadModel query,
        decimal thresholdRate,
        decimal thresholdAmount,
        CancellationToken cancellationToken = default);

    Task<int> CountExceptionsAsync(
        AdminFinancialDiscountQueryReadModel query,
        decimal thresholdRate,
        decimal thresholdAmount,
        CancellationToken cancellationToken = default);
}
