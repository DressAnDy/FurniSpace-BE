namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialExceptionRowReadModel
{
    public string ExceptionType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaymentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int Age { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public string TargetResourceType { get; set; } = string.Empty;
    public Guid TargetResourceId { get; set; }
}
