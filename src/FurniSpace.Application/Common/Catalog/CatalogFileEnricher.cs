using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Catalog;

public static class CatalogFileEnricher
{
    private const bool CustomerVisibleOnly = true;

    public static async Task EnrichProductListItemsAsync(
        List<ProductListItemDto> items,
        IProjectFileRepository files,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var productIds = items.Select(item => item.ProductId).ToList();
        var versionIds = items
            .Where(item => item.DefaultVersion is not null)
            .Select(item => item.DefaultVersion!.ProductVersionId)
            .ToList();

        var productFiles = await files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            productIds,
            CustomerVisibleOnly,
            cancellationToken);
        var versionFiles = versionIds.Count == 0
            ? Array.Empty<CatalogFileReadModel>()
            : await files.GetCatalogFilesByReferencesAsync(
                CatalogFileReferenceTypes.ProductVersion,
                versionIds,
                CustomerVisibleOnly,
                cancellationToken);

        var productFilesById = CatalogFileMapper.GroupByReferenceId(productFiles);
        var versionFilesById = CatalogFileMapper.GroupByReferenceId(versionFiles);

        foreach (var item in items)
        {
            if (productFilesById.TryGetValue(item.ProductId, out var productFileList))
            {
                item.Thumbnail = CatalogFileMapper.PickThumbnail(productFileList, CustomerVisibleOnly);
            }

            if (item.DefaultVersion is not null &&
                versionFilesById.TryGetValue(item.DefaultVersion.ProductVersionId, out var versionFileList))
            {
                item.DefaultVersion.Thumbnail = CatalogFileMapper.PickThumbnail(versionFileList, CustomerVisibleOnly);
            }
        }
    }

    public static async Task EnrichProductDetailAsync(
        ProductDetailDto detail,
        IProjectFileRepository files,
        CancellationToken cancellationToken = default)
    {
        var productFiles = await files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            [detail.ProductId],
            CustomerVisibleOnly,
            cancellationToken);
        detail.Files = CatalogFileMapper.ToList(productFiles, CustomerVisibleOnly);
        detail.Thumbnail = CatalogFileMapper.PickThumbnail(productFiles, CustomerVisibleOnly);

        var versionIds = detail.Versions.Select(version => version.ProductVersionId).ToList();
        if (versionIds.Count == 0)
        {
            return;
        }

        var versionFiles = await files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            versionIds,
            CustomerVisibleOnly,
            cancellationToken);
        var versionFilesById = CatalogFileMapper.GroupByReferenceId(versionFiles);

        foreach (var version in detail.Versions)
        {
            if (!versionFilesById.TryGetValue(version.ProductVersionId, out var versionFileList))
            {
                continue;
            }

            version.Files = CatalogFileMapper.ToList(versionFileList, CustomerVisibleOnly);
            version.Thumbnail = CatalogFileMapper.PickThumbnail(versionFileList, CustomerVisibleOnly);
        }

        if (detail.DefaultVersion is not null &&
            versionFilesById.TryGetValue(detail.DefaultVersion.ProductVersionId, out var defaultFiles))
        {
            detail.DefaultVersion.Files = CatalogFileMapper.ToList(defaultFiles, CustomerVisibleOnly);
            detail.DefaultVersion.Thumbnail = CatalogFileMapper.PickThumbnail(defaultFiles, CustomerVisibleOnly);
        }
    }
}
