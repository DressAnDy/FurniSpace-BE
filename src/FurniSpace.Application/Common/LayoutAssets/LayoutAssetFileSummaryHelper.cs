using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.Common.LayoutAssets;

internal static class LayoutAssetFileSummaryHelper
{
    internal static LayoutAssetPrimaryFileSummaryDto? PickPrimary(
        IEnumerable<CatalogFileReadModel> files,
        FileType fileType)
    {
        var candidates = files
            .Where(file => file.FileType == fileType && file.Status == FileStatus.ACTIVE)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var selected = candidates.FirstOrDefault(file => file.IsPrimary == true)
            ?? candidates.OrderByDescending(file => file.UploadedAt).First();

        return new LayoutAssetPrimaryFileSummaryDto
        {
            FileId = selected.FileId,
            Url = selected.FileUrl
        };
    }

    internal static LayoutAssetPrimaryFileSummaryDto? PickPrimaryPreview(IEnumerable<CatalogFileReadModel> files)
    {
        var candidates = files
            .Where(file =>
                file.Status == FileStatus.ACTIVE &&
                file.FileType is FileType.PREVIEW or FileType.PRODUCT_PREVIEW)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var selected = candidates.FirstOrDefault(file => file.IsPrimary == true)
            ?? candidates.OrderByDescending(file => file.FileType == FileType.PREVIEW)
                .ThenByDescending(file => file.UploadedAt)
                .First();

        return new LayoutAssetPrimaryFileSummaryDto
        {
            FileId = selected.FileId,
            Url = selected.FileUrl
        };
    }

    internal static IReadOnlyList<LayoutAssetFileDto> ToFileDtos(IEnumerable<CatalogFileReadModel> files)
    {
        return files
            .Where(file => file.Status == FileStatus.ACTIVE)
            .OrderBy(file => file.FileType)
            .ThenByDescending(file => file.IsPrimary == true)
            .ThenBy(file => file.DisplayOrder ?? int.MaxValue)
            .ThenByDescending(file => file.UploadedAt)
            .Select(file => new LayoutAssetFileDto
            {
                FileId = file.FileId,
                FileType = file.FileType,
                Url = file.FileUrl,
                FileName = file.OriginalFileName,
                MimeType = file.MimeType,
                IsPrimary = file.IsPrimary == true,
                DisplayOrder = file.DisplayOrder,
                Status = file.Status ?? FileStatus.ACTIVE
            })
            .ToList();
    }

    internal static bool IsAllowedUploadFileType(FileType fileType)
    {
        return fileType is FileType.MODEL_3D
            or FileType.TEXTURE
            or FileType.PREVIEW
            or FileType.OTHER;
    }

    internal static bool IsPrimaryEligibleFileType(FileType? fileType)
    {
        return fileType is FileType.MODEL_3D
            or FileType.TEXTURE
            or FileType.PREVIEW
            or FileType.PRODUCT_PREVIEW;
    }
}
