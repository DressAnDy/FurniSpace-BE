namespace FurniSpace.Application.DTOs.Products;

public static class CatalogFileUploadResponseMapper
{
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
