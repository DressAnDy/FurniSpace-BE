using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common;

internal static class PreviewImageFileLinkOrdering
{
    public static int ResolveDisplayOrder(
        int? requestedOrder,
        IReadOnlyList<FileLink> existingLinks,
        int existingCount)
    {
        if (!requestedOrder.HasValue)
        {
            if (existingCount == 0)
            {
                return 1;
            }

            var maxOrder = existingLinks.Max(link => link.DisplayOrder ?? 0);
            return maxOrder <= 0 ? existingCount + 1 : maxOrder + 1;
        }

        return requestedOrder.Value;
    }

    public static int ResolveInsertDisplayOrder(
        int? requestedOrder,
        IReadOnlyList<FileLink> existingLinks,
        int existingCount)
    {
        if (!requestedOrder.HasValue)
        {
            return ResolveDisplayOrder(null, existingLinks, existingCount);
        }

        if (requestedOrder.Value <= 0)
        {
            return requestedOrder.Value;
        }

        var maxInsert = existingCount + 1;
        return Math.Min(requestedOrder.Value, maxInsert);
    }

    public static void ShiftDisplayOrdersForInsert(IReadOnlyList<FileLink> existingLinks, int insertOrder)
    {
        foreach (var link in existingLinks.Where(link => (link.DisplayOrder ?? int.MaxValue) >= insertOrder))
        {
            link.DisplayOrder = (link.DisplayOrder ?? 0) + 1;
        }
    }

    public static void NormalizeDisplayOrdersAndPrimary(List<FileLink> fileLinks)
    {
        var ordered = fileLinks
            .OrderBy(link => link.DisplayOrder ?? int.MaxValue)
            .ThenBy(link => link.CreatedAt ?? DateTime.MinValue)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].DisplayOrder = index + 1;
            ordered[index].IsPrimary = index == 0;
        }
    }

    public static bool HasDuplicatePositiveDisplayOrders(IReadOnlyList<FileLink> fileLinks)
    {
        var positiveOrders = fileLinks
            .Where(link => link.DisplayOrder is > 0)
            .Select(link => link.DisplayOrder!.Value);

        return positiveOrders.Distinct().Count() != positiveOrders.Count();
    }

    public static bool TryValidateUniquePositiveDisplayOrders(
        IReadOnlyList<FileLink> fileLinks,
        out string? validationMessage)
    {
        validationMessage = null;
        if (HasDuplicatePositiveDisplayOrders(fileLinks))
        {
            validationMessage = "Preview display orders must be unique for each reference.";
            return false;
        }

        return true;
    }

    public static Error? ValidateUniquePositiveDisplayOrdersOrConflict(
        IReadOnlyList<FileLink> fileLinks,
        string errorCode)
    {
        if (!TryValidateUniquePositiveDisplayOrders(fileLinks, out var validationMessage))
        {
            return Error.Conflict(errorCode, validationMessage!);
        }

        return null;
    }

    public static void EnsureUniquePositiveDisplayOrders(IReadOnlyList<FileLink> fileLinks)
    {
        if (!TryValidateUniquePositiveDisplayOrders(fileLinks, out var validationMessage))
        {
            throw new InvalidOperationException(validationMessage);
        }
    }

    public static List<FileLink> MergePendingPreviewLink(
        IReadOnlyList<FileLink> queriedLinks,
        FileLink pendingLink)
    {
        var mergedLinks = queriedLinks.ToList();
        if (mergedLinks.All(link => link.FileId != pendingLink.FileId))
        {
            mergedLinks.Add(pendingLink);
        }

        return mergedLinks;
    }

    public static bool TryBuildExactReorderMap(
        IReadOnlyList<Guid>? fileIds,
        IReadOnlyCollection<Guid> expectedIds,
        out Dictionary<Guid, int>? orderByFileId,
        out string? validationMessage)
    {
        orderByFileId = null;
        validationMessage = null;
        var requestedIds = fileIds ?? [];

        if (requestedIds.Count != requestedIds.Distinct().Count())
        {
            validationMessage = "fileIds must not contain duplicates.";
            return false;
        }

        if (requestedIds.Count != expectedIds.Count)
        {
            validationMessage = "fileIds must include every preview image exactly once.";
            return false;
        }

        if (requestedIds.Any(fileId => !expectedIds.Contains(fileId)))
        {
            validationMessage = "fileIds must include every preview image exactly once.";
            return false;
        }

        orderByFileId = requestedIds
            .Select((fileId, index) => new { fileId, Order = index + 1 })
            .ToDictionary(item => item.fileId, item => item.Order);
        return true;
    }

    public static void ApplyReorderFromFileIds(IReadOnlyList<Guid> fileIds, IReadOnlyList<FileLink> fileLinks)
    {
        for (var index = 0; index < fileIds.Count; index++)
        {
            var link = fileLinks.Single(item => item.FileId == fileIds[index]);
            link.DisplayOrder = index + 1;
            link.IsPrimary = index == 0;
        }
    }

    public static List<ProductVersionPreviewReorderItemDto> MapProductVersionReorderItems(IReadOnlyList<FileLink> fileLinks)
        => MapReorderItems<ProductVersionPreviewReorderItemDto>(
            fileLinks,
            (link, displayOrder, isPrimary) => new ProductVersionPreviewReorderItemDto
            {
                FileId = link.FileId,
                FileLinkId = link.FileLinkId,
                DisplayOrder = displayOrder,
                IsPrimary = isPrimary
            });

    public static List<ProductPreviewReorderItemDto> MapProductPreviewReorderItems(IReadOnlyList<FileLink> fileLinks)
        => MapReorderItems<ProductPreviewReorderItemDto>(
            fileLinks,
            (link, displayOrder, isPrimary) => new ProductPreviewReorderItemDto
            {
                FileId = link.FileId,
                FileLinkId = link.FileLinkId,
                DisplayOrder = displayOrder,
                IsPrimary = isPrimary
            });

    private static List<T> MapReorderItems<T>(
        IReadOnlyList<FileLink> fileLinks,
        Func<FileLink, int, bool, T> map)
    {
        return fileLinks
            .OrderBy(link => link.DisplayOrder ?? int.MaxValue)
            .ThenBy(link => link.CreatedAt ?? DateTime.MinValue)
            .Select(link => map(
                link,
                link.DisplayOrder ?? 0,
                link.IsPrimary == true))
            .ToList();
    }
}
