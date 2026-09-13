namespace FurniSpace.Application.Common.Orders;

public sealed class OrderWorkflowSettings
{
    public const string SectionName = "OrderWorkflow";

    public int DepositPercent { get; set; } = 30;
}
