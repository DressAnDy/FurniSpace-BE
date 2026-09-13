#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Production;

public sealed class UpdateProductionItemStatusDto
{
    public ProductionItemStatus? Status { get; set; }
    public string? ProductionNote { get; set; }
    public string? CancellationReason { get; set; }
}
