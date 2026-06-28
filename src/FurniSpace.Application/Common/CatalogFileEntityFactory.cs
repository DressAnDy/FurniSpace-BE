using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Common;

internal static class CatalogFileEntityFactory
{
    public static StoredFile CreateStoredFile(
        Guid fileId,
        Guid uploadedBy,
        string originalFileName,
        string generatedFileName,
        StorageUploadResult uploadResult,
        UploadCatalogFileRequestDto request,
        DateTime uploadedAt)
    {
        return new StoredFile
        {
            FileId = fileId,
            UploadedBy = uploadedBy,
            OriginalFileName = originalFileName,
            StoredFileName = generatedFileName,
            FileUrl = uploadResult.PublicUrl,
            StoragePath = uploadResult.ObjectName,
            MimeType = CatalogFileStorageHelpers.NormalizeContentType(request.ContentType),
            FileExtension = CatalogFileStorageHelpers.NormalizeExtension(originalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = uploadedAt
        };
    }

    public static FileLink CreateFileLink(CatalogFileLinkCreationContext context)
    {
        return new FileLink
        {
            FileLinkId = context.FileLinkId,
            FileId = context.FileId,
            ReferenceType = context.ReferenceType,
            ReferenceId = context.ReferenceId,
            FileType = context.FileType,
            Visibility = context.Visibility,
            Description = CatalogFileStorageHelpers.NormalizeOptional(context.Description),
            DisplayOrder = context.DisplayOrder,
            CreatedBy = context.CreatedBy,
            CreatedAt = context.CreatedAt
        };
    }
}
