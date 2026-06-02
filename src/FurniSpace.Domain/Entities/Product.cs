using System;

namespace FurniSpace.Domain.Entities;

public class Product
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = null!;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}


