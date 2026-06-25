#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;

namespace FurniSpace.API.Tests.TestDoubles;

internal sealed class FakeProductService : IProductService
{
    private readonly ServiceResult<ProductListResponseDto> _getAllResult;
    private readonly ServiceResult<ProductByCategoryResponseDto> _getByCategoryResult;
    private readonly ServiceResult<ProductDetailDto> _getByIdResult;
    private readonly ServiceResult<ProductDto> _createResult;
    private readonly ServiceResult<ProductDto> _updateResult;
    private readonly ServiceResult<CatalogFileUploadResponseDto> _uploadFileResult;

    public FakeProductService(
        ServiceResult<ProductListResponseDto> getAllResult,
        ServiceResult<ProductByCategoryResponseDto>? getByCategoryResult = null,
        ServiceResult<ProductDetailDto>? getByIdResult = null,
        ServiceResult<ProductDto>? createResult = null,
        ServiceResult<ProductDto>? updateResult = null,
        ServiceResult<CatalogFileUploadResponseDto>? uploadFileResult = null)
    {
        _getAllResult = getAllResult;
        _getByCategoryResult = getByCategoryResult ?? ServiceResult<ProductByCategoryResponseDto>.Success(new ProductByCategoryResponseDto(), string.Empty);
        _getByIdResult = getByIdResult ?? ServiceResult<ProductDetailDto>.Success(new ProductDetailDto(), string.Empty);
        _createResult = createResult ?? ServiceResult<ProductDto>.Created(new ProductDto(), "Product master created successfully.");
        _updateResult = updateResult ?? ServiceResult<ProductDto>.Success(new ProductDto(), "Product master updated successfully.");
        _uploadFileResult = uploadFileResult ?? ServiceResult<CatalogFileUploadResponseDto>.Created(new CatalogFileUploadResponseDto(), "Product file uploaded successfully.");
    }

    public CreateProductRequestDto? CreateRequest { get; private set; }
    public UpdateProductRequestDto? UpdateRequest { get; private set; }
    public UploadCatalogFileRequestDto? UploadFileRequest { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid CurrentUserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public int Page { get; private set; }
    public int Limit { get; private set; }
    public bool IncludeDefaultVersion { get; private set; }

    public Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        CreateRequest = request;
        return Task.FromResult(_createResult);
    }

    public Task<ServiceResult<ProductDto>> UpdateAsync(
        Guid productId,
        UpdateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ProductId = productId;
        UpdateRequest = request;
        return Task.FromResult(_updateResult);
    }

    public Task<ServiceResult<ProductListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Page = page;
        Limit = limit;
        return Task.FromResult(_getAllResult);
    }

    public Task<ServiceResult<ProductDetailDto>> GetByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ProductId = productId;
        return Task.FromResult(_getByIdResult);
    }

    public Task<ServiceResult<ProductByCategoryResponseDto>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        CategoryId = categoryId;
        Page = page;
        Limit = limit;
        IncludeDefaultVersion = includeDefaultVersion;
        return Task.FromResult(_getByCategoryResult);
    }

    public Task<ServiceResult<ProductListResponseDto>> SearchAsync(
        ProductSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _ = request;
        return Task.FromResult(ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty));
    }

    public Task<ServiceResult<ProductSuggestResponseDto>> SuggestAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _ = query;
        _ = limit;
        return Task.FromResult(ServiceResult<ProductSuggestResponseDto>.Success(new ProductSuggestResponseDto(), string.Empty));
    }

    public Task<ServiceResult<ProductListResponseDto>> GetSimilarAsync(
        Guid productId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _ = productId;
        _ = limit;
        return Task.FromResult(ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty));
    }

    public Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid productId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ProductId = productId;
        CurrentUserId = currentUserId;
        UploadFileRequest = request;
        return Task.FromResult(_uploadFileResult);
    }
}
