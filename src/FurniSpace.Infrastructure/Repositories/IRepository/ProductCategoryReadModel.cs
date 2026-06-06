namespace FurniSpace.Infrastructure.Repositories.IRepository;

public sealed class ProductCategoryReadModel
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
