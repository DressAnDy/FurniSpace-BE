namespace FurniSpace.Infrastructure.Common.Search.Documents;

public sealed class ProductSearchDocument
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public string? CategoryName { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Status { get; set; }
    public bool IsPublic { get; set; }
    public DateTime? CreatedAt { get; set; }
}
