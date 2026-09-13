namespace FurniSpace.Application.Interfaces.Search;

public interface IProductSearchIndexer
{
    Task SyncProductAsync(Guid productId, CancellationToken cancellationToken = default);
}
