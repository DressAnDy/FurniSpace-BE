#nullable enable

using System;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Storage;

public sealed class CatalogFileEntityFactoryTests
{
    [Fact]
    public void CreateStoredFile_MapsUploadMetadata()
    {
        var fileId = Guid.NewGuid();
        var uploadedBy = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        var storedFile = CatalogFileEntityFactory.CreateStoredFile(
            fileId,
            uploadedBy,
            "preview.JPG",
            $"{fileId:N}.jpg",
            new StorageUploadResult
            {
                ObjectName = "products/p1/file.jpg",
                PublicUrl = "https://storage.example.com/products/p1/file.jpg",
                Bucket = "test-bucket"
            },
            new UploadCatalogFileRequestDto
            {
                OriginalFileName = "preview.JPG",
                ContentType = "image/jpeg",
                FileSizeBytes = 2048
            },
            uploadedAt);

        Assert.Equal(fileId, storedFile.FileId);
        Assert.Equal(uploadedBy, storedFile.UploadedBy);
        Assert.Equal("preview.JPG", storedFile.OriginalFileName);
        Assert.Equal("https://storage.example.com/products/p1/file.jpg", storedFile.FileUrl);
        Assert.Equal("products/p1/file.jpg", storedFile.StoragePath);
        Assert.Equal("image/jpeg", storedFile.MimeType);
        Assert.Equal("jpg", storedFile.FileExtension);
        Assert.Equal(2048, storedFile.FileSizeBytes);
        Assert.Equal(FileStatus.ACTIVE, storedFile.Status);
        Assert.Equal(uploadedAt, storedFile.UploadedAt);
    }

    [Fact]
    public void CreatePendingStoredFile_UsesEmptyUrlAndPendingStatus()
    {
        var fileId = Guid.NewGuid();

        var storedFile = CatalogFileEntityFactory.CreatePendingStoredFile(
            fileId,
            Guid.NewGuid(),
            "model.glb",
            $"{fileId:N}.glb",
            "products/p1/model.glb",
            new UploadCatalogFileRequestDto
            {
                OriginalFileName = "model.glb",
                ContentType = "model/gltf-binary",
                FileSizeBytes = 4096
            },
            DateTime.UtcNow);

        Assert.Equal(string.Empty, storedFile.FileUrl);
        Assert.Equal(FileStatus.PENDING, storedFile.Status);
        Assert.Equal("products/p1/model.glb", storedFile.StoragePath);
    }

    [Fact]
    public void CreateFileLink_MapsContextFields()
    {
        var fileLinkId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var fileLink = CatalogFileEntityFactory.CreateFileLink(new CatalogFileLinkCreationContext
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = referenceId,
            FileType = FileType.PRODUCT_PREVIEW,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            Description = "  Cover image  ",
            DisplayOrder = 1
        });

        Assert.Equal(fileLinkId, fileLink.FileLinkId);
        Assert.Equal(fileId, fileLink.FileId);
        Assert.Equal(CatalogFileReferenceTypes.Product, fileLink.ReferenceType);
        Assert.Equal(referenceId, fileLink.ReferenceId);
        Assert.Equal(FileType.PRODUCT_PREVIEW, fileLink.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, fileLink.Visibility);
        Assert.Equal("Cover image", fileLink.Description);
        Assert.Equal(1, fileLink.DisplayOrder);
        Assert.Equal(createdBy, fileLink.CreatedBy);
        Assert.Equal(createdAt, fileLink.CreatedAt);
    }
}
