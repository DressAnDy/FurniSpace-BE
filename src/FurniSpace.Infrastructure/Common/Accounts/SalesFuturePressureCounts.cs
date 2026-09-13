namespace FurniSpace.Infrastructure.Common.Accounts;

public sealed class SalesFuturePressureCounts
{
    public int MeasurementRequiredCount { get; init; }
    public int SpaceVerifiedCount { get; init; }
    public int ProposalConsultingCount { get; init; }
    public int InProductionCount { get; init; }
    public int ProductionBlockedCount { get; init; }
    public int ReadyForDeliveryCount { get; init; }
    public int DeliveringCount { get; init; }
    public int DeliveredCount { get; init; }
}
