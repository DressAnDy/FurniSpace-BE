#nullable enable

using FurniSpace.Domain.Common;

namespace FurniSpace.Infrastructure.ReadModels.Production;

public sealed class ProductionUnavailableItemReadModel : ProductionUnavailableItemShape;

public sealed class ProductionUnavailableItemsQueryReadModel
{
    public string? Keyword { get; set; }
    public Guid? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
