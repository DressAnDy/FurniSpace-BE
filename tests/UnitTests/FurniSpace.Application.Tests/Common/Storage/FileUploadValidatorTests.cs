#nullable enable

using System;
using System.IO;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Infrastructure.Common.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Storage;

public sealed class FileUploadValidatorTests
{
    [Fact]
    public void Validate_WithMissingFile_ReturnsMissingFileFailure()
    {
        var validator = CreateValidator();

        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = Stream.Null,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.MissingFile, result.FailureKind);
    }

    [Fact]
    public void Validate_WithNonReadableStream_ReturnsMissingFileFailure()
    {
        var validator = CreateValidator();

        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = new NonReadableStream(),
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.MissingFile, result.FailureKind);
    }

    [Fact]
    public void Validate_WithMissingFileName_ReturnsMissingFileNameFailure()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = " ",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.MissingFileName, result.FailureKind);
    }

    [Fact]
    public void Validate_WithInvalidFileSize_ReturnsInvalidFileSizeFailure()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 0
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.InvalidFileSize, result.FailureKind);
    }

    [Fact]
    public void Validate_WithOversizedFile_ReturnsFileTooLargeFailure()
    {
        var validator = CreateValidator(maxFileSizeBytes: 10);

        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 11
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.FileTooLarge, result.FailureKind);
    }

    [Fact]
    public void Validate_WithMissingExtension_ReturnsInvalidExtensionFailure()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.InvalidExtension, result.FailureKind);
    }

    [Fact]
    public void Validate_WithInvalidExtension_ReturnsInvalidExtensionFailure()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.exe",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.InvalidExtension, result.FailureKind);
    }

    [Fact]
    public void Validate_WithInvalidMimeType_ReturnsInvalidMimeTypeFailure()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.pdf",
            ContentType = "application/x-msdownload",
            FileSizeBytes = 1
        });

        Assert.False(result.IsValid);
        Assert.Equal(FileUploadValidationFailureKind.InvalidMimeType, result.FailureKind);
    }

    [Fact]
    public void Validate_WithValidPayload_ReturnsSuccess()
    {
        var validator = CreateValidator();

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenUploadSettingsEmpty_UsesFirebaseMaxFileSizeAndDefaults()
    {
        var validator = new FileUploadValidator(
            Options.Create(new FileUploadSettings()),
            Options.Create(new FirebaseStorageSettings { MaxFileSizeBytes = 2048 }));

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenAllowedListsEmpty_UsesDefaultExtensionsAndMimeTypes()
    {
        var validator = new FileUploadValidator(
            Options.Create(new FileUploadSettings
            {
                MaxFileSizeBytes = 1024,
                AllowedExtensions = [],
                AllowedMimeTypes = []
            }),
            Options.Create(new FirebaseStorageSettings()));

        using var stream = new MemoryStream([1]);
        var result = validator.Validate(new TestFileUploadPayload
        {
            Content = stream,
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg",
            FileSizeBytes = 1
        });

        Assert.True(result.IsValid);
    }

    private static FileUploadValidator CreateValidator(long maxFileSizeBytes = 1024 * 1024)
    {
        return new FileUploadValidator(
            Options.Create(new FileUploadSettings
            {
                MaxFileSizeBytes = maxFileSizeBytes,
                AllowedExtensions = [".pdf"],
                AllowedMimeTypes = ["application/pdf"]
            }),
            Options.Create(new FirebaseStorageSettings()));
    }

    private sealed class TestFileUploadPayload : IFileUploadPayload
    {
        public Stream Content { get; init; } = Stream.Null;
        public string OriginalFileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long FileSizeBytes { get; init; }
    }

    private sealed class NonReadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
