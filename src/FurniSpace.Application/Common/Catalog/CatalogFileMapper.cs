using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using Mapster;

namespace FurniSpace.Application.Common.Catalog;

public static class CatalogFileMapper
{
    public static CatalogFileDto? PickThumbnail(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        var visibleFiles = FilterVisible(files, customerVisibleOnly).ToList();
        if (visibleFiles.Count == 0)
        {
            return null;
        }

        var preview = visibleFiles.FirstOrDefault(file => file.FileType == FileType.PRODUCT_PREVIEW);
        return (preview ?? visibleFiles[0]).Adapt<CatalogFileDto>();
    }

    public static List<CatalogFileDto> ToList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return FilterVisible(files, customerVisibleOnly)
            .OrderByDescending(file => file.FileType == FileType.PRODUCT_PREVIEW)
            .ThenByDescending(file => file.UploadedAt)
            .Adapt<List<CatalogFileDto>>();
    }

    public static Dictionary<Guid, List<CatalogFileReadModel>> GroupByReferenceId(
        IEnumerable<CatalogFileReadModel> files)
    {
        return files
            .GroupBy(file => file.ReferenceId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private static IEnumerable<CatalogFileReadModel> FilterVisible(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return files.Where(file =>
            file.Status == FileStatus.ACTIVE &&
            (!customerVisibleOnly || file.Visibility == FileVisibility.CUSTOMER_VISIBLE));
    }
}
