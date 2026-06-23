using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Storage;

namespace FurniSpace.Application.Common;

internal static class CatalogPreviewImageServiceHelpers
{
    public static StoredFile CreatePreviewStoredFile(
        Guid fileId,
        Guid uploadedBy,
        string originalFileName,
        string generatedFileName,
        StorageUploadResult uploadResult,
        UploadProductPreviewImageRequestDto request,
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

    public static FileLink CreateProductPreviewFileLink(
        Guid fileLinkId,
        Guid fileId,
        Guid productId,
        int displayOrder,
        Guid createdBy,
        DateTime createdAt,
        string? description)
    {
        return new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = productId,
            FileType = FileType.PRODUCT_PREVIEW,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            DisplayOrder = displayOrder,
            Description = CatalogFileStorageHelpers.NormalizeOptional(description),
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public static string BuildProductStorageObjectName(
        FirebaseStorageSettings firebaseSettings,
        Guid productId,
        string generatedFileName)
    {
        return CatalogFileStorageHelpers.BuildStorageObjectName(
            "products",
            firebaseSettings.ProductFilesPrefix,
            productId,
            generatedFileName);
    }
}
