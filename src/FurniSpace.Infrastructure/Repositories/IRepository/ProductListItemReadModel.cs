using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public sealed class ProductListItemReadModel
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProductStatus? Status { get; set; }
    public ProductVersionReadModel? DefaultVersion { get; set; }
}
