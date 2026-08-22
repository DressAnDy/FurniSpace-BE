#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.Interfaces.LayoutAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class RoomPlannerLayoutAssetsControllerTests
{
    [Fact]
    public void GetCatalog_RequiresDesignerAndAdmin()
    {
        var authorize = typeof(RoomPlannerLayoutAssetsController)
            .GetMethod(nameof(RoomPlannerLayoutAssetsController.GetCatalog))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetCatalog_PassesRoleAndQueryToService()
    {
        var response = new LayoutAssetListResponseDto { Total = 2 };
        var service = new FakeLayoutAssetService(
            catalogResult: ServiceResult<LayoutAssetListResponseDto>.Success(response));
        var controller = WithDesigner(new RoomPlannerLayoutAssetsController(service));

        var actionResult = await controller.GetCatalog(new RoomPlannerLayoutAssetCatalogQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = "stair"
        });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal("DESIGNER", service.LastRoleName);
        Assert.Equal("stair", service.LastQuery?.Search);
    }

    private static RoomPlannerLayoutAssetsController WithDesigner(RoomPlannerLayoutAssetsController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "DESIGNER")
                ],
                authenticationType: "Test"))
            }
        };
        return controller;
    }

    private sealed class FakeLayoutAssetService : ILayoutAssetService
    {
        private readonly ServiceResult<LayoutAssetListResponseDto>? _catalogResult;

        public FakeLayoutAssetService(ServiceResult<LayoutAssetListResponseDto>? catalogResult = null)
        {
            _catalogResult = catalogResult;
        }

        public string? LastRoleName { get; private set; }

        public RoomPlannerLayoutAssetCatalogQueryDto? LastQuery { get; private set; }

        public Task<ServiceResult<LayoutAssetListResponseDto>> GetRoomPlannerCatalogAsync(
            RoomPlannerLayoutAssetCatalogQueryDto query,
            string? roleName,
            CancellationToken cancellationToken = default)
        {
            LastRoleName = roleName;
            LastQuery = query;
            return Task.FromResult(_catalogResult ?? ServiceResult<LayoutAssetListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<LayoutAssetDto>> CreateAsync(
            CreateLayoutAssetRequestDto request,
            Guid currentUserId,
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

        public Task<ServiceResult<LayoutAssetListResponseDto>> GetAllAsync(
            LayoutAssetQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetListResponseDto>.Unauthorized());

        public Task<ServiceResult<LayoutAssetDto>> GetByIdAsync(
            Guid layoutAssetId,
            string? roleName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<LayoutAssetDto>.Unauthorized());

        public Task<ServiceResult<FurniSpace.Application.DTOs.Products.CatalogFileUploadResponseDto>> UploadFileAsync(
            Guid layoutAssetId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.Products.UploadCatalogFileRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.Products.CatalogFileUploadResponseDto>.Unauthorized());

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
    }
}
