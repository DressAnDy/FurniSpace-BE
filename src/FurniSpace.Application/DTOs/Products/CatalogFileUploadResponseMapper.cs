using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.DTOs.Products;

public static class CatalogFileUploadResponseMapper
{
    public static CatalogFileUploadResponseDto FromActivated(
        StoredFile storedFile,
        FileLink fileLink,
        string referenceType,
        Guid referenceId,
        StorageUploadResult uploadResult)
    {
        return new CatalogFileUploadResponseDto
        {
            FileId = storedFile.FileId,
            FileLinkId = fileLink.FileLinkId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            OriginalFileName = storedFile.OriginalFileName,
            FileType = fileLink.FileType ?? FileType.OTHER,
            FileUrl = uploadResult.PublicUrl,
            MimeType = storedFile.MimeType,
            FileSizeBytes = storedFile.FileSizeBytes,
            Visibility = fileLink.Visibility ?? FileVisibility.CUSTOMER_VISIBLE,
            UploadedBy = storedFile.UploadedBy,
            UploadedAt = storedFile.UploadedAt,
            Description = fileLink.Description,
            DisplayOrder = fileLink.DisplayOrder,
            IsPrimary = fileLink.IsPrimary,
            CreatedAt = fileLink.CreatedAt
        };
    }

    public static CatalogFileUploadResponseDto FromUpload(CatalogFileUploadResponseContext context)
    {
        return new CatalogFileUploadResponseDto
        {
            FileId = context.FileId,
            FileLinkId = context.FileLinkId,
            ReferenceType = context.ReferenceType,
            ReferenceId = context.ReferenceId,
            OriginalFileName = context.OriginalFileName,
            FileType = context.Request.FileType,
            FileUrl = context.UploadResult.PublicUrl,
            MimeType = context.StoredFile.MimeType,
            FileSizeBytes = context.Request.FileSizeBytes,
            Visibility = context.Visibility,
            UploadedBy = context.CurrentUserId,
            UploadedAt = context.UploadedAt,
            Description = context.FileLink.Description,
            DisplayOrder = context.FileLink.DisplayOrder,
            IsPrimary = context.FileLink.IsPrimary,
            CreatedAt = context.UploadedAt
        };
    }
}
