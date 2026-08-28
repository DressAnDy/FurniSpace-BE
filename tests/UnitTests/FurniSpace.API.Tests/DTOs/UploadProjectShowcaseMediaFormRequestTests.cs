using System.IO;
using System.Text;
using FurniSpace.API.DTOs.ProjectShowcases;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FurniSpace.API.Tests.DTOs;

public sealed class UploadProjectShowcaseMediaFormRequestTests
{
    [Fact]
    public void ToRequestDto_MapsMultipartFields()
    {
        var request = new UploadProjectShowcaseMediaFormRequest
        {
            File = CreateFormFile("showcase.webp", "image/webp", "image-bytes"),
            MediaType = ProjectShowcaseMediaType.DETAIL,
            Title = "Detail shot",
            Caption = "Completed bar area",
            SetAsCover = true
        };

        var dto = request.ToRequestDto();

        Assert.Equal("showcase.webp", dto.OriginalFileName);
        Assert.Equal("image/webp", dto.ContentType);
        Assert.Equal(11, dto.FileSizeBytes);
        Assert.Equal(ProjectShowcaseMediaType.DETAIL, dto.MediaType);
        Assert.Equal("Detail shot", dto.Title);
        Assert.Equal("Completed bar area", dto.Caption);
        Assert.True(dto.SetAsCover);
        Assert.True(dto.Content.CanRead);
        dto.Content.Dispose();
    }

    [Fact]
    public void ToRequestDto_WhenFileMissing_UsesDefaults()
    {
        var request = new UploadProjectShowcaseMediaFormRequest
        {
            File = null,
            MediaType = null,
            SetAsCover = false
        };

        var dto = request.ToRequestDto();

        Assert.Equal(string.Empty, dto.OriginalFileName);
        Assert.Equal("application/octet-stream", dto.ContentType);
        Assert.Equal(0, dto.FileSizeBytes);
        Assert.Equal(ProjectShowcaseMediaType.FINAL, dto.MediaType);
        Assert.Equal(Stream.Null, dto.Content);
    }

    private static FormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
