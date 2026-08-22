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

        public FakeLayoutAssetService(
            ServiceResult<LayoutAssetDto>? createResult = null,
            ServiceResult<LayoutAssetListResponseDto>? listResult = null)
        {
            _createResult = createResult;
            _listResult = listResult;
        }

        public CreateLayoutAssetRequestDto? CreateRequest { get; private set; }

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
            return Task.FromResult(_listResult ?? ServiceResult<LayoutAssetListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetDto>> GetByIdAsync(
            Guid layoutAssetId,
            string? roleName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetDto>.Unauthorized());

        public Task<ServiceResult<LayoutAssetDto>> UpdateAsync(
            Guid layoutAssetId,
            UpdateLayoutAssetRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetDto>.Unauthorized());

        public Task<ServiceResult<LayoutAssetDto>> UpdateStatusAsync(
            Guid layoutAssetId,
            UpdateLayoutAssetStatusRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetDto>.Unauthorized());

        public Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
            Guid layoutAssetId,
            Guid currentUserId,
            UploadCatalogFileRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CatalogFileUploadResponseDto>.Unauthorized());

        public Task<ServiceResult<IReadOnlyList<LayoutAssetFileDto>>> GetFilesAsync(
            Guid layoutAssetId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.Unauthorized());

        public Task<ServiceResult<LayoutAssetFilePrimaryResponseDto>> SetPrimaryFileAsync(
            Guid layoutAssetId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetFilePrimaryResponseDto>.Unauthorized());

        public Task<ServiceResult<LayoutAssetFileDto>> DeleteFileAsync(
            Guid layoutAssetId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetFileDto>.Unauthorized());

        public Task<ServiceResult<LayoutAssetListResponseDto>> GetRoomPlannerCatalogAsync(
            RoomPlannerLayoutAssetCatalogQueryDto query,
            string? roleName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetListResponseDto>.Unauthorized());
    }
}
