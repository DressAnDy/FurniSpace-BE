#nullable enable

using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.ProjectFiles;

public sealed class UploadProjectFileFormRequest
{
    public IFormFile? File { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public bool? IsPrimary { get; set; }
    public int? DisplayOrder { get; set; }
    public string? Note { get; set; }

    public UploadProjectFileRequestDto ToRequestDto()
    {
        return new UploadProjectFileRequestDto
        {
            Content = File?.OpenReadStream() ?? Stream.Null,
            OriginalFileName = File?.FileName ?? string.Empty,
            ContentType = File?.ContentType ?? "application/octet-stream",
            FileSizeBytes = File?.Length ?? 0,
            FileType = FileType,
            Visibility = Visibility,
            IsPrimary = IsPrimary,
            DisplayOrder = DisplayOrder,
            Note = Note
        };
    }
}
