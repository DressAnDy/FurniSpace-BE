using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;

namespace FurniSpace.Infrastructure.Search;

public static class ProjectFileSearchDocumentMapper
{
    public static bool IsIndexable(ProjectFileSearchIndexItemReadModel item)
    {
        return item.Status is null or FileStatus.ACTIVE &&
            !string.IsNullOrWhiteSpace(item.OriginalFileName);
    }

    public static ProjectFileSearchDocument ToDocument(ProjectFileSearchIndexItemReadModel item)
    {
        return new ProjectFileSearchDocument
        {
            FileId = item.FileId,
            FileLinkId = item.FileLinkId,
            ProjectId = item.ProjectId,
            ReferenceType = item.ReferenceType,
            ReferenceId = item.ReferenceId,
            OriginalFileName = item.OriginalFileName,
            FileType = item.FileType?.ToString(),
            Visibility = item.Visibility?.ToString(),
            MimeType = item.MimeType,
            UploadedAt = item.UploadedAt,
            UploadedBy = item.UploadedBy
        };
    }
}
