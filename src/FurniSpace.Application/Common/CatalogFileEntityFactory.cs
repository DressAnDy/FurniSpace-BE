using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Storage;

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

    public static FileLink CreateFileLink(
        Guid fileLinkId,
        Guid fileId,
        string referenceType,
        Guid referenceId,
        FileType fileType,
        FileVisibility visibility,
        Guid createdBy,
        DateTime createdAt,
        string? description,
        int? displayOrder = null)
    {
        return new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            FileType = fileType,
            Visibility = visibility,
            Description = CatalogFileStorageHelpers.NormalizeOptional(description),
            DisplayOrder = displayOrder,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }
}
