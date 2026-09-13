#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Catalog;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductPreviewFilesControllerTests
{
    [Fact]
    public void Reorder_RequiresAdminRole()
    {
        var authorize = typeof(ProductPreviewFilesController)
            .GetMethod(nameof(ProductPreviewFilesController.Reorder))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Reorder_PassesRequestToPreviewService()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var previewService = new FakeProductPreviewImageService
        {
            ReorderResult = ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Success(
                [
                    new ProductPreviewReorderItemDto
                    {
                        FileId = fileId,
                        FileLinkId = Guid.NewGuid(),
                        DisplayOrder = 1,
                        IsPrimary = true
                    }
                ],
                "Product preview images reordered successfully.")
        };
        var controller = new ProductPreviewFilesController(previewService);
        var request = new ReorderProductPreviewImagesRequestDto { FileIds = [fileId] };

        var actionResult = await controller.Reorder(productId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Product preview images reordered successfully.", result.Message);
        Assert.Equal(productId, previewService.ProductId);
        Assert.NotNull(previewService.ReorderRequest);
        Assert.Equal(fileId, Assert.Single(previewService.ReorderRequest.FileIds!));
    }

    [Fact]
    public void Delete_RequiresAdminRole()
    {
        var authorize = typeof(ProductPreviewFilesController)
            .GetMethod(nameof(ProductPreviewFilesController.Delete))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Delete_PassesIdsToPreviewService()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var previewService = new FakeProductPreviewImageService
        {
            DeleteResult = ServiceResult<DeleteProductPreviewImageResponseDto>.Success(
                new DeleteProductPreviewImageResponseDto
                {
                    DeletedFileId = fileId,
                    RemainingCount = 2,
                    Reindexed = true
                },
                "Product preview image deleted successfully.")
        };
        var controller = new ProductPreviewFilesController(previewService);

        var actionResult = await controller.Delete(productId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(productId, previewService.ProductId);
        Assert.Equal(fileId, previewService.FileId);
    }

    private sealed class FakeProductPreviewImageService : IProductPreviewImageService
    {
        public ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>> ReorderResult { get; init; } =
            ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Success(
                [],
                "Product preview images reordered successfully.");

        public ServiceResult<DeleteProductPreviewImageResponseDto> DeleteResult { get; init; } =
            ServiceResult<DeleteProductPreviewImageResponseDto>.Success(
                new DeleteProductPreviewImageResponseDto(),
                "Product preview image deleted successfully.");

        public Guid? ProductId { get; private set; }
        public Guid? FileId { get; private set; }
        public ReorderProductPreviewImagesRequestDto? ReorderRequest { get; private set; }

        public Task<ServiceResult<ProductPreviewImageUploadResponseDto>> UploadAsync(
            Guid productId,
            Guid currentUserId,
            UploadProductPreviewImageRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>> ReorderAsync(
            Guid productId,
            ReorderProductPreviewImagesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductId = productId;
            ReorderRequest = request;
            return Task.FromResult(ReorderResult);
        }

        public Task<ServiceResult<DeleteProductPreviewImageResponseDto>> DeleteAsync(
            Guid productId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            ProductId = productId;
            FileId = fileId;
            return Task.FromResult(DeleteResult);
        }
    }
}
