#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;

namespace FurniSpace.API.Tests.TestDoubles;

public sealed class FakeProductPreviewImageService : IProductPreviewImageService
{
    public ServiceResult<PrepareDirectUploadResponseDto>? PrepareUploadResult { get; init; }
    public ServiceResult<ProductPreviewImageUploadResponseDto>? CompleteUploadResult { get; init; }
    public ServiceResult<ProductPreviewImageListResponseDto>? GetListResult { get; init; }
    public ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>? ReorderResult { get; init; }
    public ServiceResult<DeleteProductPreviewImageResponseDto>? DeleteResult { get; init; }

    public Guid? ProductId { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public Guid? FileId { get; private set; }
    public UploadProductPreviewImageRequestDto? PrepareUploadRequest { get; private set; }
    public CompleteDirectUploadRequestDto? CompleteUploadRequest { get; private set; }
    public ReorderProductPreviewImagesRequestDto? ReorderRequest { get; private set; }

    public Task<ServiceResult<PrepareDirectUploadResponseDto>> PreparePreviewUploadAsync(
        Guid productId,
        Guid currentUserId,
        UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        CurrentUserId = currentUserId;
        PrepareUploadRequest = request;
        return Task.FromResult(PrepareUploadResult ?? ServiceResult<PrepareDirectUploadResponseDto>.BadRequest("Prepare upload not configured."));
    }

    public Task<ServiceResult<ProductPreviewImageUploadResponseDto>> CompletePreviewUploadAsync(
        Guid productId,
        Guid currentUserId,
        CompleteDirectUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        CurrentUserId = currentUserId;
        CompleteUploadRequest = request;
        return Task.FromResult(CompleteUploadResult ?? ServiceResult<ProductPreviewImageUploadResponseDto>.BadRequest("Complete upload not configured."));
    }

    public Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        return Task.FromResult(GetListResult ?? ServiceResult<ProductPreviewImageListResponseDto>.BadRequest("Get list not configured."));
    }

    public Task<ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>> ReorderAsync(
        Guid productId,
        ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ProductId = productId;
        ReorderRequest = request;
        return Task.FromResult(ReorderResult ?? ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.BadRequest("Reorder not configured."));
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
