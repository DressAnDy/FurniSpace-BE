#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Catalog;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductVersionsControllerTests
{
    [Theory]
    [InlineData(nameof(ProductVersionsController.Update))]
    [InlineData(nameof(ProductVersionsController.SetDefault))]
    public void AdminOnlyMutations_RequireAdminRole(string methodName)
    {
        var authorize = typeof(ProductVersionsController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void PrepareFileUpload_RequiresDesignerAndAdminRoles()
    {
        var authorize = typeof(ProductVersionsController)
            .GetMethods()
            .Single(method => method.Name == nameof(ProductVersionsController.PrepareFileUpload))
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public void Create_RequiresDesignerAndAdminRoles()
    {
        var authorize = typeof(ProductVersionsController)
            .GetMethods()
            .Single(method => method.Name == nameof(ProductVersionsController.Create))
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Create_ReturnsCreatedServiceResultThroughBaseController()
    {
        var productId = Guid.NewGuid();
        var response = new ProductVersionDto
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            VersionCode = "PV",
            VersionName = "Version",
            Status = ProductStatus.ACTIVE
        };
        var service = new FakeProductVersionService(
            createResult: ServiceResult<ProductVersionDto>.Created(response, "Product version created successfully."));
        var controller = CreateController(service, Guid.NewGuid(), "ADMIN");
        var request = new CreateProductVersionRequestDto
        {
            VersionCode = "PV",
            VersionName = "Version"
        };

        var actionResult = await controller.Create(productId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductVersionDto>>(objectResult.Value);
        Assert.Equal(201, result.Status);
        Assert.Equal("Product version created successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(productId, service.ProductId);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultThroughBaseController()
    {
        var productVersionId = Guid.NewGuid();
        var response = new ProductVersionDto
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV",
            VersionName = "Updated",
            Status = ProductStatus.ACTIVE
        };
        var service = new FakeProductVersionService(
            updateResult: ServiceResult<ProductVersionDto>.Success(response, "Product version updated successfully."));
        var controller = new ProductVersionsController(service);
        var request = new UpdateProductVersionRequestDto
        {
            VersionName = "Updated"
        };

        var actionResult = await controller.Update(productVersionId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductVersionDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Product version updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(productVersionId, service.ProductVersionId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task GetListByProduct_ReturnsServiceResultThroughBaseController()
    {
        var productId = Guid.NewGuid();
        var response = new ProductVersionListResponseDto { Page = 1, PageSize = 20, TotalCount = 0 };
        var service = new FakeProductVersionService(
            getListResult: ServiceResult<ProductVersionListResponseDto>.Success(response, string.Empty));
        var controller = new ProductVersionsController(service);

        var actionResult = await controller.GetListByProduct(productId, new ProductVersionListQueryDto
        {
            Page = 1,
            PageSize = 20
        });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(productId, service.ProductId);
    }

    [Theory]
    [InlineData(nameof(ProductVersionsController.Activate))]
    [InlineData(nameof(ProductVersionsController.Deactivate))]
    [InlineData(nameof(ProductVersionsController.Archive))]
    [InlineData(nameof(ProductVersionsController.Restore))]
    public async Task LifecycleActions_ReturnServiceResultThroughBaseController(string actionName)
    {
        var productVersionId = Guid.NewGuid();
        var service = new FakeProductVersionService();
        var controller = new ProductVersionsController(service);
        var method = typeof(ProductVersionsController).GetMethod(actionName)!;

        var actionResult = await (Task<IActionResult>)method.Invoke(controller, [productVersionId, CancellationToken.None])!;

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task SetDefault_ReturnsServiceResultThroughBaseController()
    {
        var productVersionId = Guid.NewGuid();
        var response = new SetDefaultProductVersionDto
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            IsDefault = true
        };
        var service = new FakeProductVersionService(
            setDefaultResult: ServiceResult<SetDefaultProductVersionDto>.Success(
                response,
                "Default product version updated successfully."));
        var controller = new ProductVersionsController(service);

        var actionResult = await controller.SetDefault(productVersionId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<SetDefaultProductVersionDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Default product version updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(productVersionId, service.ProductVersionId);
    }

    [Fact]
    public async Task PrepareFileUpload_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProductVersionService();
        var controller = new ProductVersionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.PrepareFileUpload(Guid.NewGuid(), new UploadCatalogFileRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProductVersionId);
    }

    [Fact]
    public async Task PrepareFileUpload_PassesRequestToService()
    {
        var currentUserId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var response = new CatalogFileUploadResponseDto
        {
            FileId = Guid.NewGuid(),
            ReferenceType = "PRODUCT_VERSION",
            ReferenceId = productVersionId,
            FileType = FileType.PRODUCT_PREVIEW
        };
        var prepareResponse = new PrepareDirectUploadResponseDto { FileId = response.FileId };
        var service = new FakeProductVersionService(
            prepareFileUploadResult: ServiceResult<PrepareDirectUploadResponseDto>.Created(
                prepareResponse,
                "Catalog file upload URL created successfully."));
        var controller = CreateController(service, currentUserId);
        var request = new UploadCatalogFileRequestDto
        {
            OriginalFileName = "chair-preview.webp",
            ContentType = "image/webp",
            FileSizeBytes = 12,
            FileType = FileType.PRODUCT_PREVIEW,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            Description = "Preview image",
            DisplayOrder = 2
        };

        var actionResult = await controller.PrepareFileUpload(productVersionId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<PrepareDirectUploadResponseDto>>(objectResult.Value);
        Assert.Same(prepareResponse, result.Data);
        Assert.Equal(productVersionId, service.ProductVersionId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.PrepareFileUploadRequest);
        Assert.Equal("chair-preview.webp", service.PrepareFileUploadRequest.OriginalFileName);
        Assert.Equal("image/webp", service.PrepareFileUploadRequest.ContentType);
        Assert.Equal(FileType.PRODUCT_PREVIEW, service.PrepareFileUploadRequest.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, service.PrepareFileUploadRequest.Visibility);
        Assert.Equal("Preview image", service.PrepareFileUploadRequest.Description);
        Assert.Equal(2, service.PrepareFileUploadRequest.DisplayOrder);
    }

    [Fact]
    public async Task CompleteFileUpload_PassesRequestToService()
    {
        var currentUserId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeProductVersionService(
            completeFileUploadResult: ServiceResult<CatalogFileUploadResponseDto>.Created(
                new CatalogFileUploadResponseDto
                {
                    FileId = fileId,
                    ReferenceType = "PRODUCT_VERSION",
                    ReferenceId = productVersionId
                },
                "Product version file uploaded successfully."));
        var controller = CreateController(service, currentUserId);

        var actionResult = await controller.CompleteFileUpload(
            productVersionId,
            new CompleteDirectUploadRequestDto { FileId = fileId });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(productVersionId, service.ProductVersionId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Equal(fileId, service.CompleteFileUploadRequest!.FileId);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResultThroughBaseController()
    {
        var productVersionId = Guid.NewGuid();
        var response = new ProductVersionDetailDto
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-001",
            VersionName = "Standard",
            Status = ProductStatus.ACTIVE
        };
        var service = new FakeProductVersionService(
            getByIdResult: ServiceResult<ProductVersionDetailDto>.Success(response, string.Empty));
        var controller = new ProductVersionsController(service);

        var actionResult = await controller.GetById(productVersionId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProductVersionDetailDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(productVersionId, service.ProductVersionId);
    }

    private sealed class FakeProductVersionService : IProductVersionService
    {
        private readonly ServiceResult<ProductVersionDto> _createResult;
        private readonly ServiceResult<ProductVersionDto> _updateResult;
        private readonly ServiceResult<SetDefaultProductVersionDto> _setDefaultResult;
        private readonly ServiceResult<ProductVersionDetailDto> _getByIdResult;
        private readonly ServiceResult<PrepareDirectUploadResponseDto> _prepareFileUploadResult;
        private readonly ServiceResult<CatalogFileUploadResponseDto> _completeFileUploadResult;
        private readonly ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>> _reorderPreviewResult;
        private readonly ServiceResult<DeleteProductVersionPreviewImageResponseDto> _deletePreviewResult;
        private readonly ServiceResult<ProductVersionListResponseDto>? _getListResult;

        public FakeProductVersionService(
            ServiceResult<ProductVersionDto>? createResult = null,
            ServiceResult<ProductVersionDto>? updateResult = null,
            ServiceResult<SetDefaultProductVersionDto>? setDefaultResult = null,
            ServiceResult<ProductVersionDetailDto>? getByIdResult = null,
            ServiceResult<PrepareDirectUploadResponseDto>? prepareFileUploadResult = null,
            ServiceResult<CatalogFileUploadResponseDto>? completeFileUploadResult = null,
            ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>? reorderPreviewResult = null,
            ServiceResult<DeleteProductVersionPreviewImageResponseDto>? deletePreviewResult = null,
            ServiceResult<ProductVersionListResponseDto>? getListResult = null)
        {
            _createResult = createResult ?? ServiceResult<ProductVersionDto>.Created(
                new ProductVersionDto(),
                "Product version created successfully.");
            _updateResult = updateResult ?? ServiceResult<ProductVersionDto>.Success(
                new ProductVersionDto(),
                "Product version updated successfully.");
            _setDefaultResult = setDefaultResult ?? ServiceResult<SetDefaultProductVersionDto>.Success(
                new SetDefaultProductVersionDto(),
                "Default product version updated successfully.");
            _getByIdResult = getByIdResult ?? ServiceResult<ProductVersionDetailDto>.Success(
                new ProductVersionDetailDto(),
                string.Empty);
            _prepareFileUploadResult = prepareFileUploadResult ?? ServiceResult<PrepareDirectUploadResponseDto>.Created(
                new PrepareDirectUploadResponseDto(),
                "Catalog file upload URL created successfully.");
            _completeFileUploadResult = completeFileUploadResult ?? ServiceResult<CatalogFileUploadResponseDto>.Created(
                new CatalogFileUploadResponseDto(),
                "Product version file uploaded successfully.");
            _reorderPreviewResult = reorderPreviewResult ?? ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Success(
                [],
                "Product version preview images reordered successfully.");
            _deletePreviewResult = deletePreviewResult ?? ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Success(
                new DeleteProductVersionPreviewImageResponseDto(),
                "Product version preview image deleted successfully.");
            _getListResult = getListResult;
        }

        public Guid ProductId { get; private set; }
        public Guid ProductVersionId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid FileId { get; private set; }
        public CreateProductVersionRequestDto? CreateRequest { get; private set; }
        public UpdateProductVersionRequestDto? UpdateRequest { get; private set; }
        public UploadCatalogFileRequestDto? PrepareFileUploadRequest { get; private set; }
        public CompleteDirectUploadRequestDto? CompleteFileUploadRequest { get; private set; }
        public ReorderProductVersionPreviewFilesRequestDto? ReorderPreviewRequest { get; private set; }

        public Task<ServiceResult<ProductVersionDto>> CreateAsync(
            Guid productId,
            CreateProductVersionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductId = productId;
            CreateRequest = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<ProductVersionDto>> UpdateAsync(
            Guid productVersionId,
            UpdateProductVersionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            UpdateRequest = request;
            return Task.FromResult(_updateResult);
        }

        public Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            return Task.FromResult(_setDefaultResult);
        }

        public Task<ServiceResult<ProductVersionDetailDto>> GetByIdAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            return Task.FromResult(_getByIdResult);
        }

        public Task<ServiceResult<PrepareDirectUploadResponseDto>> PrepareFileUploadAsync(
            Guid productVersionId,
            Guid currentUserId,
            UploadCatalogFileRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            CurrentUserId = currentUserId;
            PrepareFileUploadRequest = request;
            return Task.FromResult(_prepareFileUploadResult);
        }

        public Task<ServiceResult<CatalogFileUploadResponseDto>> CompleteFileUploadAsync(
            Guid productVersionId,
            Guid currentUserId,
            CompleteDirectUploadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            CurrentUserId = currentUserId;
            CompleteFileUploadRequest = request;
            return Task.FromResult(_completeFileUploadResult);
        }

        public Task<ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>> ReorderPreviewFilesAsync(
            Guid productVersionId,
            ReorderProductVersionPreviewFilesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            ReorderPreviewRequest = request;
            return Task.FromResult(_reorderPreviewResult);
        }

        public Task<ServiceResult<DeleteProductVersionPreviewImageResponseDto>> DeletePreviewFileAsync(
            Guid productVersionId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            ProductVersionId = productVersionId;
            FileId = fileId;
            return Task.FromResult(_deletePreviewResult);
        }

        public Task<ServiceResult<ProductVersionListResponseDto>> GetListByProductAsync(
            Guid productId,
            ProductVersionListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProductId = productId;
            return Task.FromResult(_getListResult ?? ServiceResult<ProductVersionListResponseDto>.Success(
                new ProductVersionListResponseDto(),
                string.Empty));
        }

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ActivateAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductVersionLifecycleStatusResponseDto>.Success(
                new ProductVersionLifecycleStatusResponseDto(),
                "Product version lifecycle updated successfully."));

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> DeactivateAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductVersionLifecycleStatusResponseDto>.Success(
                new ProductVersionLifecycleStatusResponseDto(),
                "Product version lifecycle updated successfully."));

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ArchiveAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductVersionLifecycleStatusResponseDto>.Success(
                new ProductVersionLifecycleStatusResponseDto(),
                "Product version lifecycle updated successfully."));

        public Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> RestoreAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductVersionLifecycleStatusResponseDto>.Success(
                new ProductVersionLifecycleStatusResponseDto(),
                "Product version lifecycle updated successfully."));
    }

    private static ProductVersionsController CreateController(
        FakeProductVersionService service,
        Guid currentUserId,
        string? role = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, currentUserId.ToString())
        };
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ProductVersionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private static FormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
