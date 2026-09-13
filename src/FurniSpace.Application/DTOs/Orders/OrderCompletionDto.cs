#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderCompletionDto
{
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
