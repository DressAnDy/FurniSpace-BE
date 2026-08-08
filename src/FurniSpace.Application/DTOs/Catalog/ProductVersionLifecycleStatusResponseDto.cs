using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProductVersionLifecycleStatusResponseDto
{
    public Guid ProductVersionId { get; set; }
    public Guid ProductId { get; set; }
    public ProductStatus? PreviousStatus { get; set; }
    public ProductStatus? Status { get; set; }
    public bool? IsDefault { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
