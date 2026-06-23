using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;

namespace FurniSpace.Application.Common;

internal static class CatalogFileOrdering
{
    public static int PreviewDisplayOrderSortKey(int? displayOrder)
    {
        if (displayOrder is null or <= 0)
        {
            return int.MaxValue;
        }

        return displayOrder.Value;
    }

    public static IEnumerable<CatalogFileReadModel> SortCatalogFiles(IEnumerable<CatalogFileReadModel> files)
    {
        return files
            .OrderByDescending(file => file.FileType == FileType.PRODUCT_PREVIEW)
            .ThenBy(file => file.FileType == FileType.PRODUCT_PREVIEW
                ? PreviewDisplayOrderSortKey(file.DisplayOrder)
                : int.MaxValue)
            .ThenByDescending(file => file.UploadedAt);
    }

    public static CatalogFileReadModel? PickPreviewThumbnail(IEnumerable<CatalogFileReadModel> files)
    {
        var previews = files
            .Where(file => file.FileType == FileType.PRODUCT_PREVIEW)
            .ToList();
        if (previews.Count == 0)
        {
            return null;
        }

        var primary = previews.FirstOrDefault(file => file.IsPrimary == true);
        if (primary is not null)
        {
            return primary;
        }

        return SortCatalogFiles(previews).First();
    }
}
