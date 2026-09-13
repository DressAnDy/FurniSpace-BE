#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestStatusDto
{
    public Guid ProductionRequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? ActualStartDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
