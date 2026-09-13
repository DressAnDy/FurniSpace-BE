using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProductLifecycleStatusResponseDto
{
    public Guid ProductId { get; set; }
    public ProductStatus? PreviousStatus { get; set; }
    public ProductStatus? Status { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ActiveVersionCount { get; set; }
}
