#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.API.Tests.TestDoubles;
using FurniSpace.Application.Common;
using System.IO;
using System.Security.Claims;
using FurniSpace.Application.DTOs.Products;
using Microsoft.AspNetCore.Http;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductsControllerTests
{
    [Fact]
    public void Create_RequiresAdminRole()
    {
        var method = typeof(ProductsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(ProductsController.Create));

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void Update_RequiresAdminRole()
    {
        var method = typeof(ProductsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(ProductsController.Update));

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetAll_DoesNotRequireAuthorization()
    {
        var method = typeof(ProductsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(ProductsController.GetAll));

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.Null(authorize);
    }

    [Fact]
    public async Task Create_ReturnsServiceResultThroughBaseController()
    {
        var response = new ProductDto
        {
            ProductId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            ProductCode = "PM-COUNTER-001",
            ProductName = "Coffee Counter",
            Description = "Counter template for cafe projects",
            Status = ProductStatus.ACTIVE
        };
        var service = new FakeProductService(
            getAllResult: ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty),
            getByCategoryResult: ServiceResult<ProductByCategoryResponseDto>.Success(new ProductByCategoryResponseDto(), string.Empty),
            getByIdResult: ServiceResult<ProductDetailDto>.Success(new ProductDetailDto(), string.Empty),
            createResult: ServiceResult<ProductDto>.Created(response, "Product master created successfully."));
        var controller = new ProductsController(service, new FakeProductPreviewImageService());
        var request = new CreateProductRequestDto
        {
            CategoryId = response.CategoryId!.Value,
            ProductCode = "PM-COUNTER-001",
            ProductName = "Coffee Counter",
            Description = "Counter template for cafe projects"
        };

        var actionResult = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductDto>>(objectResult.Value);
        Assert.Equal(201, result.Status);
        Assert.Equal("Product master created successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultThroughBaseController()
    {
        var productId = Guid.NewGuid();
        var response = new ProductDto
        {
            ProductId = productId,
            CategoryId = Guid.NewGuid(),
            ProductCode = "PM-COUNTER-001",
            ProductName = "Coffee Counter Updated",
            Description = "Updated counter template for cafe projects",
            Status = ProductStatus.ACTIVE
        };
        var service = new FakeProductService(
            getAllResult: ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty),
            getByCategoryResult: ServiceResult<ProductByCategoryResponseDto>.Success(new ProductByCategoryResponseDto(), string.Empty),
            getByIdResult: ServiceResult<ProductDetailDto>.Success(new ProductDetailDto(), string.Empty),
            updateResult: ServiceResult<ProductDto>.Success(response, "Product master updated successfully."));
        var controller = new ProductsController(service, new FakeProductPreviewImageService());
        var request = new UpdateProductRequestDto
        {
            CategoryId = response.CategoryId!.Value,
            ProductName = "Coffee Counter Updated",
            Description = "Updated counter template for cafe projects"
        };

        var actionResult = await controller.Update(productId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Product master updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(productId, service.ProductId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task GetAll_ReturnsServiceResultThroughBaseController()
    {
        var response = new ProductListResponseDto
        {
            Page = 1,
            Limit = 20,
            Total = 1,
            Items =
            [
                new ProductListItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Coffee Counter",
                    DefaultVersion = new ProductVersionSummaryDto
                    {
                        ProductVersionId = Guid.NewGuid(),
                        VersionCode = "PV-COUNTER-001-V1",
                        VersionName = "Coffee Counter - Standard Wood",
                        Status = ProductStatus.ACTIVE,
                        IsPublic = true
                    }
                }
            ]
        };
        var service = new FakeProductService(ServiceResult<ProductListResponseDto>.Success(response, string.Empty));
        var controller = new ProductsController(service, new FakeProductPreviewImageService());

        var actionResult = await controller.GetAll(page: 1, limit: 20);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductListResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(1, service.Page);
        Assert.Equal(20, service.Limit);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResultThroughBaseController()
    {
        var productId = Guid.NewGuid();
        var response = new ProductDetailDto
        {
            ProductId = productId,
            ProductName = "Coffee Counter",
            Versions =
            [
                new ProductVersionSummaryDto
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionCode = "PV-COUNTER-001-V1",
                    VersionName = "Coffee Counter - Standard Wood"
                }
            ]
        };
        var service = new FakeProductService(
            getAllResult: ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty),
            getByCategoryResult: ServiceResult<ProductByCategoryResponseDto>.Success(new ProductByCategoryResponseDto(), string.Empty),
            getByIdResult: ServiceResult<ProductDetailDto>.Success(response, string.Empty));
        var controller = new ProductsController(service, new FakeProductPreviewImageService());

        var actionResult = await controller.GetById(productId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductDetailDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Same(response, result.Data);
        Assert.Equal(productId, service.ProductId);
    }

    [Fact]
    public async Task GetByCategory_ReturnsServiceResultThroughBaseController()
    {
        var categoryId = Guid.NewGuid();
        var response = new ProductByCategoryResponseDto
        {
            Category = new ProductCategorySummaryDto
            {
                CategoryId = categoryId,
                CategoryName = "Counter"
            },
            Page = 1,
            Limit = 20,
            Total = 1,
            Items =
            [
                new ProductListItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Coffee Counter"
                }
            ]
        };
        var service = new FakeProductService(
            getAllResult: ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty),
            getByCategoryResult: ServiceResult<ProductByCategoryResponseDto>.Success(response, string.Empty));
        var controller = new ProductsController(service, new FakeProductPreviewImageService());

        var actionResult = await controller.GetByCategory(
            categoryId,
            page: 1,
            limit: 20,
            includeDefaultVersion: false);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductByCategoryResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Same(response, result.Data);
        Assert.Equal(categoryId, service.CategoryId);
        Assert.Equal(1, service.Page);
        Assert.Equal(20, service.Limit);
        Assert.False(service.IncludeDefaultVersion);
    }

    [Fact]
    public void UploadFile_RequiresAdminRole()
    {
        var method = typeof(ProductsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(ProductsController.UploadFile));

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task UploadFile_PassesRequestToProductService()
    {
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var response = new CatalogFileUploadResponseDto
        {
            FileId = Guid.NewGuid(),
            ReferenceType = "PRODUCT",
            ReferenceId = productId,
            FileType = FileType.PRODUCT_PREVIEW
        };
        var service = new FakeProductService(
            getAllResult: ServiceResult<ProductListResponseDto>.Success(new ProductListResponseDto(), string.Empty),
            uploadFileResult: ServiceResult<CatalogFileUploadResponseDto>.Created(
                response,
                "Product file uploaded successfully."));
        var controller = CreateController(service, userId);
        var request = new UploadCatalogFileFormRequest
        {
            File = CreateFormFile("lamp-preview.jpg", "image/jpeg", "file-content"),
            FileType = FileType.PRODUCT_PREVIEW,
            Description = "Preview image"
        };

        var actionResult = await controller.UploadFile(productId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<CatalogFileUploadResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(productId, service.ProductId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.NotNull(service.UploadFileRequest);
        Assert.Equal("lamp-preview.jpg", service.UploadFileRequest.OriginalFileName);
        Assert.Equal(FileType.PRODUCT_PREVIEW, service.UploadFileRequest.FileType);
        Assert.Equal("Preview image", service.UploadFileRequest.Description);
    }

    private static ProductsController CreateController(FakeProductService service, Guid userId)
    {
        var controller = new ProductsController(service, new FakeProductPreviewImageService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "Test"))
            }
        };

        return controller;
    }

    private static FormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
