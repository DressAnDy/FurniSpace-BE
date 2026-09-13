using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class Product
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = null!;
    public string? Description { get; set; }
    public ProductStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
