namespace FurniSpace.Infrastructure.ReadModels.Products;

public sealed class ProjectCatalogProductListItemReadModel
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public IReadOnlyList<ProjectCatalogEligibleVersionReadModel> EligibleVersions { get; set; } = [];
}
