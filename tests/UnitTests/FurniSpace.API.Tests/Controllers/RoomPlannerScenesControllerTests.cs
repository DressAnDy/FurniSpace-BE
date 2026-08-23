#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.Interfaces.RoomPlanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class RoomPlannerScenesControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(RoomPlannerScenesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(RoomPlannerScenesController.GetScene), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(RoomPlannerScenesController.ResolveProducts), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(RoomPlannerScenesController.ResolveLayoutAssets), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(RoomPlannerScenesController.SaveScene), "DESIGNER,ADMIN")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var method = typeof(RoomPlannerScenesController)
            .GetMethods()
            .Single(method => method.Name == actionName);
        var authorize = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task GetScene_ReturnsServiceResultAndPassesCurrentUser()
    {
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new RoomPlannerSceneResponseDto { SceneId = sceneId };
        var service = new FakeRoomPlannerSceneService(
            getResult: ServiceResult<RoomPlannerSceneResponseDto>.Success(
                response,
                "Room Planner scene retrieved successfully."));
        var controller = BuildController(service, currentUserId, "DESIGNER");

        var actionResult = await controller.GetScene(sceneId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<RoomPlannerSceneResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(sceneId, service.SceneId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Equal("DESIGNER", service.CurrentUserRole);
    }

    [Fact]
    public async Task SaveScene_ReturnsServiceResultAndPassesPayload()
    {
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new RoomPlannerScenePayloadDto { SchemaVersion = 2 };
        var response = new RoomPlannerSceneSaveResponseDto { SceneId = sceneId, MongoSceneId = "mongo-id" };
        var service = new FakeRoomPlannerSceneService(
            saveResult: ServiceResult<RoomPlannerSceneSaveResponseDto>.Success(
                response,
                "Room Planner scene saved successfully."));
        var controller = BuildController(service, currentUserId, "ADMIN");

        var actionResult = await controller.SaveScene(sceneId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<RoomPlannerSceneSaveResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(sceneId, service.SceneId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Equal("ADMIN", service.CurrentUserRole);
        Assert.Same(request, service.SaveRequest);
    }

    [Fact]
    public async Task ResolveProducts_ReturnsServiceResultAndPassesCurrentUser()
    {
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var response = new ResolveRoomPlannerProductsResponseDto
        {
            SceneId = sceneId,
            Items =
            [
                new RoomPlannerResolvedProductDto
                {
                    ProductVersionId = productVersionId,
                    VersionName = "Chair"
                }
            ]
        };
        var service = new FakeRoomPlannerSceneService(
            resolveResult: ServiceResult<ResolveRoomPlannerProductsResponseDto>.Success(
                response,
                "Room planner products resolved successfully."));
        var controller = BuildController(service, currentUserId, "CUSTOMER");

        var actionResult = await controller.ResolveProducts(
            sceneId,
            new ResolveRoomPlannerProductsRequestDto { ProductVersionIds = [productVersionId] });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ResolveRoomPlannerProductsResponseDto>>(objectResult.Value);
        Assert.Single(result.Data!.Items);
        Assert.Equal(sceneId, service.SceneId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Equal("CUSTOMER", service.CurrentUserRole);
    }

    [Fact]
    public async Task Actions_WithoutUserClaim_ReturnUnauthorized()
    {
        var service = new FakeRoomPlannerSceneService();
        var controller = BuildController(service);

        Assert.IsType<UnauthorizedResult>(await controller.GetScene(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.ResolveProducts(
            Guid.NewGuid(),
            new ResolveRoomPlannerProductsRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.ResolveLayoutAssets(
            Guid.NewGuid(),
            new ResolveRoomPlannerLayoutAssetsRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.SaveScene(Guid.NewGuid(), new RoomPlannerScenePayloadDto()));
        Assert.Equal(0, service.CallCount);
    }

    private static RoomPlannerScenesController BuildController(
        IRoomPlannerSceneService service,
        Guid? currentUserId = null,
        string? roleName = null)
    {
        var claims = currentUserId.HasValue
            ? new[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString()),
                new Claim(ClaimTypes.Role, roleName ?? string.Empty)
            }
            : [];

        return new RoomPlannerScenesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    private sealed class FakeRoomPlannerSceneService : IRoomPlannerSceneService
    {
        private readonly ServiceResult<RoomPlannerSceneResponseDto> _getResult;
        private readonly ServiceResult<RoomPlannerSceneSaveResponseDto> _saveResult;
        private readonly ServiceResult<ResolveRoomPlannerProductsResponseDto> _resolveResult;
        private readonly ServiceResult<ResolveRoomPlannerLayoutAssetsResponseDto> _resolveLayoutAssetsResult;

        public FakeRoomPlannerSceneService(
            ServiceResult<RoomPlannerSceneResponseDto>? getResult = null,
            ServiceResult<RoomPlannerSceneSaveResponseDto>? saveResult = null,
            ServiceResult<ResolveRoomPlannerProductsResponseDto>? resolveResult = null,
            ServiceResult<ResolveRoomPlannerLayoutAssetsResponseDto>? resolveLayoutAssetsResult = null)
        {
            _getResult = getResult ?? ServiceResult<RoomPlannerSceneResponseDto>.Success(new RoomPlannerSceneResponseDto());
            _saveResult = saveResult ?? ServiceResult<RoomPlannerSceneSaveResponseDto>.Success(new RoomPlannerSceneSaveResponseDto());
            _resolveResult = resolveResult ?? ServiceResult<ResolveRoomPlannerProductsResponseDto>.Success(new ResolveRoomPlannerProductsResponseDto());
            _resolveLayoutAssetsResult = resolveLayoutAssetsResult ?? ServiceResult<ResolveRoomPlannerLayoutAssetsResponseDto>.Success(new ResolveRoomPlannerLayoutAssetsResponseDto());
        }

        public int CallCount { get; private set; }
        public Guid SceneId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public string? CurrentUserRole { get; private set; }
        public RoomPlannerScenePayloadDto? SaveRequest { get; private set; }

        public Task<ServiceResult<RoomPlannerSceneSaveResponseDto>> SaveSceneAsync(
            Guid sceneId,
            RoomPlannerScenePayloadDto request,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken cancellationToken = default)
        {
            Capture(sceneId, currentUserId, currentUserRole);
            SaveRequest = request;
            return Task.FromResult(_saveResult);
        }

        public Task<ServiceResult<RoomPlannerSceneResponseDto>> GetSceneAsync(
            Guid sceneId,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken cancellationToken = default)
        {
            Capture(sceneId, currentUserId, currentUserRole);
            return Task.FromResult(_getResult);
        }

        public Task<ServiceResult<ResolveRoomPlannerProductsResponseDto>> ResolveProductsAsync(
            Guid sceneId,
            ResolveRoomPlannerProductsRequestDto request,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken cancellationToken = default)
        {
            Capture(sceneId, currentUserId, currentUserRole);
            return Task.FromResult(_resolveResult);
        }

        public Task<ServiceResult<ResolveRoomPlannerLayoutAssetsResponseDto>> ResolveLayoutAssetsAsync(
            Guid sceneId,
            ResolveRoomPlannerLayoutAssetsRequestDto request,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken cancellationToken = default)
        {
            Capture(sceneId, currentUserId, currentUserRole);
            return Task.FromResult(_resolveLayoutAssetsResult);
        }

        private void Capture(Guid sceneId, Guid currentUserId, string currentUserRole)
        {
            CallCount++;
            SceneId = sceneId;
            CurrentUserId = currentUserId;
            CurrentUserRole = currentUserRole;
        }
    }
}
