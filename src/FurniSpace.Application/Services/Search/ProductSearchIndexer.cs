using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public sealed class ProductSearchIndexer : IProductSearchIndexer
{
    private const string ProductIndexName = "products";

    private readonly IProductRepository _products;
    private readonly ISearchIndexService _search;

    public ProductSearchIndexer(
        IProductRepository products,
        ISearchIndexService search)
    {
        _products = products;
        _search = search;
    }

    public async Task SyncProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _products.GetSearchIndexItemAsync(productId, cancellationToken);
            if (item is null || !ProductSearchDocumentMapper.IsIndexable(item))
            {
                await _search.DeleteAsync(ProductIndexName, productId.ToString(), cancellationToken);
                return;
            }

            var document = ProductSearchDocumentMapper.ToDocument(item);
            await _search.IndexAsync(ProductIndexName, productId.ToString(), document, cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }
}
