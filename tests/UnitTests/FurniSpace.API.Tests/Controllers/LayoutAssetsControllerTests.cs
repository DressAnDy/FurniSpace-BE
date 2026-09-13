#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Catalog;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.LayoutAssets;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class LayoutAssetsControllerTests
{
    [Fact]
    public void Create_RequiresAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(LayoutAssetsController.Create));

        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetById_AllowsAdminAndDesigner()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(LayoutAssetsController.GetById));

        Assert.Equal("ADMIN,DESIGNER", authorize.Roles);
    }

    [Fact]
    public async Task Create_ReturnsServiceResult()
    {
        var response = new LayoutAssetDto { LayoutAssetId = Guid.NewGuid(), AssetCode = "STAIR-001" };
        var service = new FakeLayoutAssetService(
            createResult: ServiceResult<LayoutAssetDto>.Created(response, "Created"));
        var controller = WithUser(new LayoutAssetsController(service), "ADMIN");

        var actionResult = await controller.Create(
            new CreateLayoutAssetRequestDto
            {
                AssetCode = "STAIR-001",
                AssetName = "Straight Stair",
                AssetType = LayoutAssetType.STAIR
            });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.NotNull(service.CreateRequest);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_AllowsAnonymousList()
    {
        var service = new FakeLayoutAssetService(
            listResult: ServiceResult<LayoutAssetListResponseDto>.Success(new LayoutAssetListResponseDto()));
        var controller = new LayoutAssetsController(service);

        var actionResult = await controller.GetAll(new LayoutAssetQueryDto());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResult()
    {
        var assetId = Guid.NewGuid();
        var service = new FakeLayoutAssetService(
            getByIdResult: ServiceResult<LayoutAssetDto>.Success(new LayoutAssetDto { LayoutAssetId = assetId }));
        var controller = WithUser(new LayoutAssetsController(service), "DESIGNER");

        var actionResult = await controller.GetById(assetId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(assetId, service.LastLayoutAssetId);
    }

    [Fact]
    public async Task Update_AndUpdateStatus_ReturnServiceResults()
    {
        var assetId = Guid.NewGuid();
        var service = new FakeLayoutAssetService(
            updateResult: ServiceResult<LayoutAssetDto>.Success(new LayoutAssetDto { LayoutAssetId = assetId }),
            updateStatusResult: ServiceResult<LayoutAssetDto>.Success(new LayoutAssetDto { LayoutAssetId = assetId }));
        var controller = WithUser(new LayoutAssetsController(service), "ADMIN");

        var updateResult = await controller.Update(
            assetId,
            new UpdateLayoutAssetRequestDto { AssetName = "Updated", AssetType = LayoutAssetType.STAIR });
        var statusResult = await controller.UpdateStatus(
            assetId,
            new UpdateLayoutAssetStatusRequestDto { Status = LayoutAssetStatus.INACTIVE });

        Assert.Equal(200, Assert.IsType<ObjectResult>(updateResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(statusResult).StatusCode);
    }

    [Fact]
    public async Task GetFiles_SetPrimary_AndDeleteFile_ReturnServiceResults()
    {
        var assetId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeLayoutAssetService(
            filesResult: ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.Success([]),
            setPrimaryResult: ServiceResult<LayoutAssetFilePrimaryResponseDto>.Success(new LayoutAssetFilePrimaryResponseDto()),
            deleteFileResult: ServiceResult<LayoutAssetFileDto>.Success(new LayoutAssetFileDto()));
        var controller = WithUser(new LayoutAssetsController(service), "ADMIN");

        var filesResult = await controller.GetFiles(assetId);
        var primaryResult = await controller.SetPrimaryFile(assetId, fileId);
        var deleteResult = await controller.DeleteFile(assetId, fileId);

        Assert.Equal(200, Assert.IsType<ObjectResult>(filesResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(primaryResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(deleteResult).StatusCode);
    }

    [Fact]
    public async Task GetAll_WithQueryFilters_PassesQueryToService()
    {
        var service = new FakeLayoutAssetService(
            listResult: ServiceResult<LayoutAssetListResponseDto>.Success(new LayoutAssetListResponseDto()));
        var controller = new LayoutAssetsController(service);

        var query = new LayoutAssetQueryDto
        {
            AssetType = LayoutAssetType.STAIR,
            Status = LayoutAssetStatus.ACTIVE,
            Search = "stair",
            Page = 2,
            PageSize = 10
        };
        var actionResult = await controller.GetAll(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.NotNull(service.LastQuery);
        Assert.Equal(LayoutAssetType.STAIR, service.LastQuery.AssetType);
        Assert.Equal(LayoutAssetStatus.ACTIVE, service.LastQuery.Status);
        Assert.Equal("stair", service.LastQuery.Search);
        Assert.Equal(2, service.LastQuery.Page);
        Assert.Equal(10, service.LastQuery.PageSize);
    }

    [Fact]
    public async Task Create_WithoutUser_ReturnsUnauthorized()
    {
        var controller = new LayoutAssetsController(new FakeLayoutAssetService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.Create(new CreateLayoutAssetRequestDto
        {
            AssetCode = "STAIR-001",
            AssetName = "Stair",
            AssetType = LayoutAssetType.STAIR
        });

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static AuthorizeAttribute GetMethodAuthorizeAttribute(string methodName)
    {
        return typeof(LayoutAssetsController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
    }

    private static LayoutAssetsController WithUser(LayoutAssetsController controller, string role)
    {
        var userId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, role)
                ],
                authenticationType: "Test"))
            }
        };
        return controller;
    }

    private sealed class FakeLayoutAssetService : ILayoutAssetService
    {
        private readonly ServiceResult<LayoutAssetDto>? _createResult;
        private readonly ServiceResult<LayoutAssetListResponseDto>? _listResult;
        private readonly ServiceResult<LayoutAssetDto>? _getByIdResult;
        private readonly ServiceResult<LayoutAssetDto>? _updateResult;
        private readonly ServiceResult<LayoutAssetDto>? _updateStatusResult;
        private readonly ServiceResult<IReadOnlyList<LayoutAssetFileDto>>? _filesResult;
        private readonly ServiceResult<LayoutAssetFilePrimaryResponseDto>? _setPrimaryResult;
        private readonly ServiceResult<LayoutAssetFileDto>? _deleteFileResult;

        public FakeLayoutAssetService(
            ServiceResult<LayoutAssetDto>? createResult = null,
            ServiceResult<LayoutAssetListResponseDto>? listResult = null,
            ServiceResult<LayoutAssetDto>? getByIdResult = null,
            ServiceResult<LayoutAssetDto>? updateResult = null,
            ServiceResult<LayoutAssetDto>? updateStatusResult = null,
            ServiceResult<IReadOnlyList<LayoutAssetFileDto>>? filesResult = null,
            ServiceResult<LayoutAssetFilePrimaryResponseDto>? setPrimaryResult = null,
            ServiceResult<LayoutAssetFileDto>? deleteFileResult = null)
        {
            _createResult = createResult;
            _listResult = listResult;
            _getByIdResult = getByIdResult;
            _updateResult = updateResult;
            _updateStatusResult = updateStatusResult;
            _filesResult = filesResult;
            _setPrimaryResult = setPrimaryResult;
            _deleteFileResult = deleteFileResult;
        }

        public CreateLayoutAssetRequestDto? CreateRequest { get; private set; }

        public LayoutAssetQueryDto? LastQuery { get; private set; }

        public Guid LastLayoutAssetId { get; private set; }

        public Task<ServiceResult<LayoutAssetDto>> CreateAsync(
            CreateLayoutAssetRequestDto request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(_createResult ?? ServiceResult<LayoutAssetDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetListResponseDto>> GetAllAsync(
            LayoutAssetQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_listResult ?? ServiceResult<LayoutAssetListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetDto>> GetByIdAsync(
            Guid layoutAssetId,
            string? roleName,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_getByIdResult ?? ServiceResult<LayoutAssetDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetDto>> UpdateAsync(
            Guid layoutAssetId,
            UpdateLayoutAssetRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_updateResult ?? ServiceResult<LayoutAssetDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetDto>> UpdateStatusAsync(
            Guid layoutAssetId,
            UpdateLayoutAssetStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_updateStatusResult ?? ServiceResult<LayoutAssetDto>.Unauthorized());
        }

        public Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
            Guid layoutAssetId,
            Guid currentUserId,
            UploadCatalogFileRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CatalogFileUploadResponseDto>.Unauthorized());

        public Task<ServiceResult<IReadOnlyList<LayoutAssetFileDto>>> GetFilesAsync(
            Guid layoutAssetId,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_filesResult ?? ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetFilePrimaryResponseDto>> SetPrimaryFileAsync(
            Guid layoutAssetId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_setPrimaryResult ?? ServiceResult<LayoutAssetFilePrimaryResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetFileDto>> DeleteFileAsync(
            Guid layoutAssetId,
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            LastLayoutAssetId = layoutAssetId;
            return Task.FromResult(_deleteFileResult ?? ServiceResult<LayoutAssetFileDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetListResponseDto>> GetRoomPlannerCatalogAsync(
            RoomPlannerLayoutAssetCatalogQueryDto query,
            string? roleName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetListResponseDto>.Unauthorized());
    }
}
