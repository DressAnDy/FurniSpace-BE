#nullable enable

using System;
using System.IO;
using System.Text;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.Common.Storage;
using Xunit;

namespace FurniSpace.Application.Tests.Common;

public sealed class CatalogPreviewFileValidationTests
{
    private static readonly ProductPreviewImageSettings DefaultSettings = ProductPreviewImageSettings.CreateDefault();

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    public void ValidateFileContent_WithAllowedImageMimeTypes_ReturnsNull(string mimeType)
    {
        var error = CatalogPreviewFileValidation.ValidateFileContent(
            new MemoryStream([1, 2, 3]),
            "preview.jpg",
            mimeType,
            3,
            DefaultSettings,
            ProductPreviewImageErrorCodes.InvalidFileType,
            ProductPreviewImageErrorCodes.FileTooLarge);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateFileContent_WithPdfMimeType_ReturnsInvalidFileType()
    {
        var error = CatalogPreviewFileValidation.ValidateFileContent(
            new MemoryStream([1, 2, 3]),
            "catalog.pdf",
            "application/pdf",
            3,
            DefaultSettings,
            ProductPreviewImageErrorCodes.InvalidFileType,
            ProductPreviewImageErrorCodes.FileTooLarge);

        Assert.NotNull(error);
        Assert.Equal(ProductPreviewImageErrorCodes.InvalidFileType, error!.Code);
        Assert.Equal(415, error.Status);
    }

    [Fact]
    public void ValidateFileContent_WithOversizedFile_ReturnsFileTooLarge()
    {
        var settings = new ProductPreviewImageSettings { MaxFileSizeBytes = 10 };

        var error = CatalogPreviewFileValidation.ValidateFileContent(
            new MemoryStream(Encoding.UTF8.GetBytes("012345678901")),
            "preview.webp",
            "image/webp",
            11,
            settings,
            ProductPreviewImageErrorCodes.InvalidFileType,
            ProductPreviewImageErrorCodes.FileTooLarge);

        Assert.NotNull(error);
        Assert.Equal(ProductPreviewImageErrorCodes.FileTooLarge, error!.Code);
        Assert.Equal(413, error.Status);
    }

    [Fact]
    public void ResolveEffectiveSettings_WhenConfiguredEmpty_UsesImageDefaults()
    {
        var effective = CatalogPreviewFileValidation.ResolveEffectiveSettings(new ProductPreviewImageSettings());

        Assert.Equal(ProductPreviewImageSettings.DefaultMaxFileSizeBytes, effective.MaxFileSizeBytes);
        Assert.Contains("image/webp", effective.AllowedMimeTypes);
        Assert.Contains(".webp", effective.AllowedExtensions);
    }
}
