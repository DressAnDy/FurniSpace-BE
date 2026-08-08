namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesFuturePressureBreakdownDto
{
    public int MeasurementRequiredCount { get; set; }
    public int SpaceVerifiedCount { get; set; }
    public int ProposalConsultingCount { get; set; }
    public int InProductionCount { get; set; }
    public int ProductionBlockedCount { get; set; }
    public int ReadyForDeliveryCount { get; set; }
    public int DeliveringCount { get; set; }
    public int DeliveredCount { get; set; }
}
