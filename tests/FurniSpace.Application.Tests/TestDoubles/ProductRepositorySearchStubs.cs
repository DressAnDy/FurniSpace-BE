#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.Tests.TestDoubles;

internal static class ProductRepositorySearchStubs
{
    public static Task<ProductListItemReadModel?> GetSearchIndexItemAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        _ = productId;
        _ = cancellationToken;
        return Task.FromResult<ProductListItemReadModel?>(null);
    }

    public static Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = page;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
    }

    public static Task<ProductSearchResultReadModel> SearchPublicAsync(
        ProductSearchQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        _ = query;
        _ = cancellationToken;
        return Task.FromResult(new ProductSearchResultReadModel());
    }

    public static Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = query;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
    }

    public static Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(
        Guid productId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = productId;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
    }
}
