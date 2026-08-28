#nullable enable

using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.MeasurementImages;

public sealed class UploadMeasurementImageFormRequest
{
    public IFormFile? File { get; set; }
    public FileVisibility? Visibility { get; set; }
    public string? Note { get; set; }
    public Guid? ProjectAreaId { get; set; }

    public UploadMeasurementImageRequestDto ToRequestDto()
    {
        return new UploadMeasurementImageRequestDto
        {
            Content = File?.OpenReadStream() ?? Stream.Null,
            OriginalFileName = File?.FileName ?? string.Empty,
            ContentType = File?.ContentType ?? "application/octet-stream",
            FileSizeBytes = File?.Length ?? 0,
            Visibility = Visibility,
            Note = Note,
            ProjectAreaId = ProjectAreaId
        };
    }
}
