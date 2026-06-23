#nullable enable

using System;
using System.Linq;
using FurniSpace.Application.Common;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using Xunit;

namespace FurniSpace.Application.Tests.Common;

public sealed class CatalogFileOrderingTests
{
    private static readonly DateTime BaseTime = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SortCatalogFiles_OrdersPreviewsByDisplayOrderThenUploadedAt()
    {
        var file1 = CreatePreview("a.jpg", displayOrder: 1, uploadedAt: BaseTime);
        var file2 = CreatePreview("b.jpg", displayOrder: 2, uploadedAt: BaseTime.AddMinutes(-5));
        var file3 = CreatePreview("c.jpg", displayOrder: 3, uploadedAt: BaseTime.AddMinutes(-10));
        var model3d = CreateFile(FileType.MODEL_3D, "model.glb", uploadedAt: BaseTime.AddHours(1));

        var sorted = CatalogFileOrdering.SortCatalogFiles([file3, model3d, file1, file2]).ToList();

        Assert.Equal(4, sorted.Count);
        Assert.Equal(file1.FileId, sorted[0].FileId);
        Assert.Equal(file2.FileId, sorted[1].FileId);
        Assert.Equal(file3.FileId, sorted[2].FileId);
        Assert.Equal(model3d.FileId, sorted[3].FileId);
    }

    [Fact]
    public void SortCatalogFiles_FallsBackToUploadedAtWhenDisplayOrderMissing()
    {
        var newer = CreatePreview("newer.jpg", displayOrder: null, uploadedAt: BaseTime);
        var older = CreatePreview("older.jpg", displayOrder: 0, uploadedAt: BaseTime.AddHours(-1));
        var ordered = CreatePreview("first.jpg", displayOrder: 1, uploadedAt: BaseTime.AddHours(-2));

        var sorted = CatalogFileOrdering.SortCatalogFiles([older, newer, ordered]).ToList();

        Assert.Equal(ordered.FileId, sorted[0].FileId);
        Assert.Equal(newer.FileId, sorted[1].FileId);
        Assert.Equal(older.FileId, sorted[2].FileId);
    }

    [Fact]
    public void PickPreviewThumbnail_PrefersIsPrimaryOverDisplayOrder()
    {
        var primary = CreatePreview("primary.jpg", displayOrder: 3, isPrimary: true, uploadedAt: BaseTime);
        var firstOrder = CreatePreview("first.jpg", displayOrder: 1, uploadedAt: BaseTime);

        var thumbnail = CatalogFileOrdering.PickPreviewThumbnail([firstOrder, primary]);

        Assert.Equal(primary.FileId, thumbnail!.FileId);
    }

    [Fact]
    public void PickPreviewThumbnail_UsesDisplayOrderOneWhenNoPrimary()
    {
        var first = CreatePreview("first.jpg", displayOrder: 1, uploadedAt: BaseTime.AddHours(-1));
        var second = CreatePreview("second.jpg", displayOrder: 2, uploadedAt: BaseTime);

        var thumbnail = CatalogFileOrdering.PickPreviewThumbnail([second, first]);

        Assert.Equal(first.FileId, thumbnail!.FileId);
    }

    private static CatalogFileReadModel CreatePreview(
        string fileName,
        int? displayOrder,
        DateTime uploadedAt,
        bool? isPrimary = null)
    {
        return CreateFile(FileType.PRODUCT_PREVIEW, fileName, uploadedAt, displayOrder, isPrimary);
    }

    private static CatalogFileReadModel CreateFile(
        FileType fileType,
        string fileName,
        DateTime uploadedAt,
        int? displayOrder = null,
        bool? isPrimary = null)
    {
        return new CatalogFileReadModel
        {
            FileId = Guid.NewGuid(),
            FileLinkId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            ReferenceType = "PRODUCT",
            FileType = fileType,
            OriginalFileName = fileName,
            FileUrl = $"https://storage.example.com/{fileName}",
            MimeType = fileType == FileType.PRODUCT_PREVIEW ? "image/jpeg" : "model/gltf-binary",
            FileSizeBytes = 1024,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            Status = FileStatus.ACTIVE,
            DisplayOrder = displayOrder,
            IsPrimary = isPrimary,
            UploadedAt = uploadedAt
        };
    }
}
