#nullable enable

using System;
using System.Collections.Generic;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using Xunit;

namespace FurniSpace.Application.Tests.Common;

public sealed class PreviewImageFileLinkOrderingTests
{
    [Fact]
    public void HasDuplicatePositiveDisplayOrders_WhenOrdersAreUnique_ReturnsFalse()
    {
        var links = new List<FileLink>
        {
            CreateLink(displayOrder: 1),
            CreateLink(displayOrder: 2),
            CreateLink(displayOrder: 3)
        };

        Assert.False(PreviewImageFileLinkOrdering.HasDuplicatePositiveDisplayOrders(links));
    }

    [Fact]
    public void HasDuplicatePositiveDisplayOrders_WhenOrdersDuplicate_ReturnsTrue()
    {
        var links = new List<FileLink>
        {
            CreateLink(displayOrder: 1),
            CreateLink(displayOrder: 2),
            CreateLink(displayOrder: 2)
        };

        Assert.True(PreviewImageFileLinkOrdering.HasDuplicatePositiveDisplayOrders(links));
    }

    [Fact]
    public void HasDuplicatePositiveDisplayOrders_IgnoresNullAndZeroOrders()
    {
        var links = new List<FileLink>
        {
            CreateLink(displayOrder: null),
            CreateLink(displayOrder: 0),
            CreateLink(displayOrder: 1)
        };

        Assert.False(PreviewImageFileLinkOrdering.HasDuplicatePositiveDisplayOrders(links));
    }

    [Fact]
    public void ValidateUniquePositiveDisplayOrdersOrConflict_ReturnsConflictWhenDuplicate()
    {
        var links = new List<FileLink>
        {
            CreateLink(displayOrder: 1),
            CreateLink(displayOrder: 1)
        };

        var error = PreviewImageFileLinkOrdering.ValidateUniquePositiveDisplayOrdersOrConflict(
            links,
            ProductPreviewImageErrorCodes.DuplicateDisplayOrder);

        Assert.NotNull(error);
        Assert.Equal(ProductPreviewImageErrorCodes.DuplicateDisplayOrder, error!.Code);
    }

    [Fact]
    public void NormalizeDisplayOrdersAndPrimary_AssignsUniqueSequentialOrders()
    {
        var first = CreateLink(displayOrder: 5);
        var second = CreateLink(displayOrder: 1);
        var third = CreateLink(displayOrder: null);
        var links = new List<FileLink> { first, second, third };

        PreviewImageFileLinkOrdering.NormalizeDisplayOrdersAndPrimary(links);

        Assert.Equal(1, second.DisplayOrder);
        Assert.True(second.IsPrimary);
        Assert.Equal(2, first.DisplayOrder);
        Assert.False(first.IsPrimary);
        Assert.Equal(3, third.DisplayOrder);
        Assert.False(PreviewImageFileLinkOrdering.HasDuplicatePositiveDisplayOrders(links));
    }

    [Fact]
    public void ApplyReorderFromFileIds_AssignsUniqueOrdersAndPrimary()
    {
        var first = CreateLink(displayOrder: 3);
        var second = CreateLink(displayOrder: 1);
        var links = new List<FileLink> { first, second };

        PreviewImageFileLinkOrdering.ApplyReorderFromFileIds([second.FileId, first.FileId], links);

        Assert.Equal(1, second.DisplayOrder);
        Assert.True(second.IsPrimary);
        Assert.Equal(2, first.DisplayOrder);
        Assert.False(first.IsPrimary);
        Assert.False(PreviewImageFileLinkOrdering.HasDuplicatePositiveDisplayOrders(links));
    }

    [Fact]
    public void MergePendingPreviewLink_AddsPendingLinkWhenQueryMissesIt()
    {
        var existing = CreateLink(displayOrder: 1);
        var pending = CreateLink(displayOrder: 2);

        var merged = PreviewImageFileLinkOrdering.MergePendingPreviewLink([existing], pending);

        Assert.Equal(2, merged.Count);
        Assert.Contains(pending, merged);
    }

    [Fact]
    public void MergePendingPreviewLink_DoesNotDuplicateExistingLink()
    {
        var existing = CreateLink(displayOrder: 1);

        var merged = PreviewImageFileLinkOrdering.MergePendingPreviewLink([existing], existing);

        Assert.Single(merged);
    }

    private static FileLink CreateLink(int? displayOrder)
    {
        return new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ReferenceType = "PRODUCT",
            ReferenceId = Guid.NewGuid(),
            DisplayOrder = displayOrder
        };
    }
}
