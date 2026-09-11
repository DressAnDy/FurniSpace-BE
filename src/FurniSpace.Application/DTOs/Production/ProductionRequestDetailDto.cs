#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestDetailDto : ProductionRequestDtoBase
{
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? Note { get; set; }
    public List<ProductionItemDto> Items { get; set; } = [];
}
