namespace FurniSpace.Infrastructure.ReadModels.Products;

public class ProjectCatalogEligibleVersionReadModel : ProjectCatalogVersionSummaryModelBase
{
    public Guid ProductId { get; set; }
    public Guid? ProjectId { get; set; }
}
