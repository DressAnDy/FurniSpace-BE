using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Common;

internal static class CatalogPreviewUploadValidation
{
    public static Error? ValidateMetadata(
        UploadCatalogFileRequestDto request,
        ProductPreviewImageSettings settings,
        string invalidFileTypeCode,
        string fileTooLargeCode)
    {
        return CatalogPreviewFileValidation.ValidateMetadata(
            request.OriginalFileName,
            request.ContentType,
            request.FileSizeBytes,
            settings,
            invalidFileTypeCode,
            fileTooLargeCode);
    }

    public static Error? ValidateMetadata(
        UploadProductPreviewImageRequestDto request,
        ProductPreviewImageSettings settings,
        string invalidFileTypeCode,
        string fileTooLargeCode)
    {
        return CatalogPreviewFileValidation.ValidateMetadata(
            request.OriginalFileName,
            request.ContentType,
            request.FileSizeBytes,
            settings,
            invalidFileTypeCode,
            fileTooLargeCode);
    }

    public static Error? ValidateDisplayOrderGreaterThanZero(
        int? displayOrder,
        string invalidDisplayOrderCode)
    {
        if (displayOrder is <= 0)
        {
            return Error.BadRequest(
                invalidDisplayOrderCode,
                "Display order must be greater than zero.");
        }

        return null;
    }

    public static Error? ValidateDisplayOrderInRange(
        int? displayOrder,
        int maxCount,
        string invalidDisplayOrderCode)
    {
        if (displayOrder is <= 0 || displayOrder > maxCount)
        {
            return Error.BadRequest(
                invalidDisplayOrderCode,
                $"Display order must be between 1 and {maxCount}.");
        }

        return null;
    }
}
