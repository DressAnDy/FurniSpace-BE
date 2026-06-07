using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.ProductVersions;

public sealed class ProductVersionService : IProductVersionService
{
    private readonly IProductVersionRepository _productVersions;

    public ProductVersionService(IProductVersionRepository productVersions)
    {
        _productVersions = productVersions;
    }

    public async Task<ServiceResult<ProductVersionDto>> CreateAsync(
        Guid productId,
        CreateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductVersionDto>.BadRequest("Product id is required.");
        }

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductVersionDto>.BadRequest(errors);
        }

        if (!await _productVersions.ProductExistsAsync(productId, cancellationToken))
        {
            return ServiceResult<ProductVersionDto>.NotFound("Product not found.");
        }

        var versionCode = request.VersionCode.Trim();
        if (await _productVersions.VersionCodeExistsAsync(versionCode, cancellationToken))
        {
            return ServiceResult<ProductVersionDto>.Conflict("Product version code already exists.");
        }

        var productVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            VersionCode = versionCode,
            VersionName = request.VersionName.Trim(),
            VersionType = request.VersionType ?? ProductVersionType.STANDARD,
            Material = NormalizeOptional(request.Material),
            Color = NormalizeOptional(request.Color),
            Width = request.Width,
            Height = request.Height,
            Depth = request.Depth,
            EstimatedPrice = request.EstimatedPrice,
            IsDefault = request.IsDefault ?? false,
            IsPublic = request.IsPublic ?? true,
            IsProjectSpecific = request.IsProjectSpecific ?? false
        };

        if (productVersion.IsDefault == true)
        {
            await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        }

        await _productVersions.AddAsync(productVersion, cancellationToken);
        await _productVersions.SaveChangesAsync(cancellationToken);
        productVersion.Status ??= ProductStatus.ACTIVE;

        return ServiceResult<ProductVersionDto>.Created(
            productVersion.Adapt<ProductVersionDto>(),
            "Product version created successfully.");
    }

    public async Task<ServiceResult<ProductVersionDto>> UpdateAsync(
        Guid productVersionId,
        UpdateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<ProductVersionDto>.BadRequest("Product version id is required.");
        }

        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductVersionDto>.BadRequest(errors);
        }

        var productVersion = await _productVersions.GetByIdAsync(productVersionId, cancellationToken);
        if (productVersion is null)
        {
            return ServiceResult<ProductVersionDto>.NotFound("Product version not found.");
        }

        productVersion.VersionName = request.VersionName.Trim();
        productVersion.VersionType = request.VersionType ?? ProductVersionType.STANDARD;
        productVersion.Material = NormalizeOptional(request.Material);
        productVersion.Color = NormalizeOptional(request.Color);
        productVersion.Width = request.Width;
        productVersion.Height = request.Height;
        productVersion.Depth = request.Depth;
        productVersion.EstimatedPrice = request.EstimatedPrice;
        productVersion.IsPublic = request.IsPublic ?? true;
        productVersion.IsProjectSpecific = request.IsProjectSpecific ?? false;
        productVersion.Status ??= ProductStatus.ACTIVE;

        if (request.IsDefault == true)
        {
            await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        }
        else
        {
            productVersion.IsDefault = request.IsDefault ?? false;
        }

        await _productVersions.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductVersionDto>.Success(
            productVersion.Adapt<ProductVersionDto>(),
            "Product version updated successfully.");
    }

    public async Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<SetDefaultProductVersionDto>.BadRequest("Product version id is required.");
        }

        var productVersion = await _productVersions.GetByIdAsync(productVersionId, cancellationToken);
        if (productVersion is null)
        {
            return ServiceResult<SetDefaultProductVersionDto>.NotFound("Product version not found.");
        }

        await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        productVersion.IsDefault = true;
        await _productVersions.SaveChangesAsync(cancellationToken);

        return ServiceResult<SetDefaultProductVersionDto>.Success(
            new SetDefaultProductVersionDto
            {
                ProductVersionId = productVersion.ProductVersionId,
                ProductId = productVersion.ProductId,
                IsDefault = productVersion.IsDefault == true
            },
            "Default product version updated successfully.");
    }

    private static List<string> ValidateCreateRequest(CreateProductVersionRequestDto request)
    {
        var errors = ValidateCommonRequest(request.VersionName);

        if (string.IsNullOrWhiteSpace(request.VersionCode))
        {
            errors.Add("Version code is required.");
        }
        else if (request.VersionCode.Trim().Length > 50)
        {
            errors.Add("Version code must not exceed 50 characters.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateProductVersionRequestDto request)
    {
        return ValidateCommonRequest(request.VersionName);
    }

    private static List<string> ValidateCommonRequest(string versionName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(versionName))
        {
            errors.Add("Version name is required.");
        }
        else if (versionName.Trim().Length > 150)
        {
            errors.Add("Version name must not exceed 150 characters.");
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
