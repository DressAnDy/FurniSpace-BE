namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class UnassignedIntakeProjectReadModel
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
}
