#nullable enable

using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.ProjectShowcases;

public sealed class UploadProjectShowcaseMediaFormRequest
{
    public IFormFile? File { get; set; }
    public ProjectShowcaseMediaType? MediaType { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool SetAsCover { get; set; }

    public UploadProjectShowcaseMediaRequestDto ToRequestDto()
    {
        return new UploadProjectShowcaseMediaRequestDto
        {
            Content = File?.OpenReadStream() ?? Stream.Null,
            OriginalFileName = File?.FileName ?? string.Empty,
            ContentType = File?.ContentType ?? "application/octet-stream",
            FileSizeBytes = File?.Length ?? 0,
            MediaType = MediaType ?? ProjectShowcaseMediaType.FINAL,
            Title = Title,
            Caption = Caption,
            SetAsCover = SetAsCover
        };
    }
}
