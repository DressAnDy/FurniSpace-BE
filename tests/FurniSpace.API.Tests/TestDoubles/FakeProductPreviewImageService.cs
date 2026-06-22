#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;

namespace FurniSpace.API.Tests.TestDoubles;

public sealed class FakeProductPreviewImageService : IProductPreviewImageService
{
    public ServiceResult<ProductPreviewImageUploadResponseDto>? UploadResult { get; init; }
    public ServiceResult<ProductPreviewImageListResponseDto>? GetListResult { get; init; }
    public ServiceResult<ProductPreviewImageListResponseDto>? ReorderResult { get; init; }
    public ServiceResult<DeleteProductPreviewImageResponseDto>? DeleteResult { get; init; }

    public Guid? ProductId { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public Guid? FileId { get; private set; }
    public UploadProductPreviewImageRequestDto? UploadRequest { get; private set; }
    public ReorderProductPreviewImagesRequestDto? ReorderRequest { get; private set; }

    public Task<ServiceResult<ProductPreviewImageUploadResponseDto>> UploadAsync(
        Guid productId,
        Guid currentUserId,
        UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        CurrentUserId = currentUserId;
        UploadRequest = request;
        return Task.FromResult(UploadResult ?? ServiceResult<ProductPreviewImageUploadResponseDto>.BadRequest("Upload not configured."));
    }

    public Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        return Task.FromResult(GetListResult ?? ServiceResult<ProductPreviewImageListResponseDto>.BadRequest("Get list not configured."));
    }

    public Task<ServiceResult<ProductPreviewImageListResponseDto>> ReorderAsync(
        Guid productId,
        ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        ReorderRequest = request;
        return Task.FromResult(ReorderResult ?? ServiceResult<ProductPreviewImageListResponseDto>.BadRequest("Reorder not configured."));
    }

    public Task<ServiceResult<DeleteProductPreviewImageResponseDto>> DeleteAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        FileId = fileId;
        return Task.FromResult(DeleteResult ?? ServiceResult<DeleteProductPreviewImageResponseDto>.BadRequest("Delete not configured."));
    }
}
