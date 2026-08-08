#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Catalog;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductVersionPreviewFilesControllerTests
{
    [Fact]
    public void Reorder_RequiresAdminRole()
    {
        var authorize = typeof(ProductVersionPreviewFilesController)
            .GetMethod(nameof(ProductVersionPreviewFilesController.Reorder))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Reorder_PassesRequestToProductVersionService()
    {
        var productVersionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeProductVersionPreviewService
        {
            ReorderResult = ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Success(
                [
                    new ProductVersionPreviewReorderItemDto
                    {
                        FileId = fileId,
                        FileLinkId = Guid.NewGuid(),
                        DisplayOrder = 1,
                        IsPrimary = true
                    }
                ],
                "Product version preview images reordered successfully.")
        };
        var controller = new ProductVersionPreviewFilesController(service);
        var request = new ReorderProductVersionPreviewFilesRequestDto { FileIds = [fileId] };

        var actionResult = await controller.Reorder(productVersionId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Product version preview images reordered successfully.", result.Message);
        Assert.Equal(productVersionId, service.ProductVersionId);
        Assert.NotNull(service.ReorderRequest);
        Assert.Equal(fileId, Assert.Single(service.ReorderRequest.FileIds!));
    }

    [Fact]
    public void Delete_RequiresAdminRole()
    {
        var authorize = typeof(ProductVersionPreviewFilesController)
            .GetMethod(nameof(ProductVersionPreviewFilesController.Delete))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Delete_PassesIdsToProductVersionService()
    {
        var productVersionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeProductVersionPreviewService
        {
            DeleteResult = ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Success(
                new DeleteProductVersionPreviewImageResponseDto
                {
                    DeletedFileId = fileId,
                    RemainingCount = 2,
                    Reindexed = true
                },
                "Product version preview image deleted successfully.")
        };
        var controller = new ProductVersionPreviewFilesController(service);

        var actionResult = await controller.Delete(productVersionId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(productVersionId, service.ProductVersionId);
        Assert.Equal(fileId, service.FileId);
    }

    private sealed class FakeProductVersionPreviewService : IProductVersionService
    {
        public ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>> ReorderResult { get; init; } =
            ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Success(
                [],
                "Product version preview images reordered successfully.");

        public ServiceResult<DeleteProductVersionPreviewImageResponseDto> DeleteResult { get; init; } =
            ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Success(
                new DeleteProductVersionPreviewImageResponseDto(),
                "Product version preview image deleted successfully.");

        public Guid ProductVersionId { get; private set; }
        public Guid FileId { get; private set; }
        public ReorderProductVersionPreviewFilesRequestDto? ReorderRequest { get; private set; }

        public Task<ServiceResult<ProductVersionDto>> CreateAsync(
            Guid productId,
            CreateProductVersionRequestDto request,
            bool allowTaxConfiguration = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionDto>> UpdateAsync(
            Guid productVersionId,
            UpdateProductVersionRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionDetailDto>> GetByIdAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
            Guid productVersionId,
            Guid currentUserId,
            UploadCatalogFileRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>> ReorderPreviewFilesAsync(
            Guid productVersionId,
            ReorderProductVersionPreviewFilesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            ReorderRequest = request;
            return Task.FromResult(ReorderResult);
        }

        public Task<ServiceResult<DeleteProductVersionPreviewImageResponseDto>> DeletePreviewFileAsync(
            Guid productVersionId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            FileId = fileId;
            return Task.FromResult(DeleteResult);
        }

        public Task<ServiceResult<ProductVersionListResponseDto>> GetListByProductAsync(
            Guid productId,
            ProductVersionListQueryDto query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ActivateAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> DeactivateAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ArchiveAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> RestoreAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
