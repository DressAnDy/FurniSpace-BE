#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProposalsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(ProposalsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(ProposalsController.Create), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.GetListByProject), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.CreateScene), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.GetScenes), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.GetSceneDetail), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.GetDetail), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.SyncItemsFromScene), "DESIGNER,ADMIN")]
    [InlineData(nameof(ProposalsController.GetItems), "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.UpdateItem), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.DeleteItem), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.SelectFinal), "CUSTOMER")]
    [InlineData(nameof(ProposalsController.RequestRevision), "CUSTOMER")]
    [InlineData(nameof(ProposalsController.Publish), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.Update), "DESIGNER,SALES,ADMIN")]
    [InlineData(nameof(ProposalsController.UpdateScene), "DESIGNER,SALES,ADMIN")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var method = typeof(ProposalsController)
            .GetMethods()
            .Single(method => method.Name == actionName);
        var authorize = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task Create_ReturnsServiceResultAndPassesRequest()
    {
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new CreateProposalRequestDto { ProposalName = "Proposal" };
        var response = new ProposalDto { ProposalId = Guid.NewGuid(), ProjectId = projectId };
        var service = new FakeProposalService(
            createResult: ServiceResult<ProposalDto>.Created(response, "Proposal created successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.Create(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task GetListByProject_ReturnsServiceResultAndPassesQuery()
    {
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProposalListResponseDto { Page = 2, Limit = 10, Total = 1 };
        var service = new FakeProposalService(
            listResult: ServiceResult<ProposalListResponseDto>.Success(
                response,
                "Project proposals retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetListByProject(
            projectId,
            ProposalStatus.DRAFT,
            page: 2,
            limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.ListQuery);
        Assert.Equal(ProposalStatus.DRAFT, service.ListQuery.Status);
        Assert.Equal(2, service.ListQuery.Page);
        Assert.Equal(10, service.ListQuery.Limit);
    }

    [Fact]
    public async Task CreateScene_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new CreateProposalSceneRequestDto
        {
            SceneName = "Main layout",
            SceneType = ProposalSceneType.THREE_D
        };
        var response = new ProposalSceneDto { SceneId = Guid.NewGuid(), ProposalId = proposalId };
        var service = new FakeProposalService(
            sceneResult: ServiceResult<ProposalSceneDto>.Created(
                response,
                "Proposal scene created successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.CreateScene(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalSceneDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.SceneRequest);
    }

    [Fact]
    public async Task GetScenes_ReturnsServiceResultAndPassesQuery()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProposalSceneListResponseDto { Page = 2, Limit = 5, Total = 1 };
        var service = new FakeProposalService(
            sceneListResult: ServiceResult<ProposalSceneListResponseDto>.Success(
                response,
                "Proposal scenes retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetScenes(
            proposalId,
            ProposalSceneType.THREE_D,
            isActive: true,
            page: 2,
            limit: 5);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalSceneListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.SceneListQuery);
        Assert.Equal(ProposalSceneType.THREE_D, service.SceneListQuery.SceneType);
        Assert.True(service.SceneListQuery.IsActive);
        Assert.Equal(2, service.SceneListQuery.Page);
        Assert.Equal(5, service.SceneListQuery.Limit);
    }

    [Fact]
    public async Task GetSceneDetail_ReturnsServiceResult()
    {
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProposalSceneDetailDto { SceneId = sceneId };
        var service = new FakeProposalService(
            sceneDetailResult: ServiceResult<ProposalSceneDetailDto>.Success(
                response,
                "Proposal scene detail retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetSceneDetail(sceneId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalSceneDetailDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(sceneId, service.SceneId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResult()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProposalDetailDto { ProposalId = proposalId };
        var service = new FakeProposalService(
            detailResult: ServiceResult<ProposalDetailDto>.Success(
                response,
                "Proposal detail retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetDetail(proposalId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalDetailDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task SyncItemsFromScene_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new SyncProposalItemsFromSceneRequestDto { SceneId = Guid.NewGuid() };
        var response = new SyncProposalItemsFromSceneResponseDto { ProposalId = proposalId, SceneId = request.SceneId };
        var service = new FakeProposalService(
            syncResult: ServiceResult<SyncProposalItemsFromSceneResponseDto>.Success(
                response,
                "Proposal items synced from scene successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.SyncItemsFromScene(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<SyncProposalItemsFromSceneResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.SyncRequest);
    }

    [Fact]
    public async Task GetItems_ReturnsServiceResultAndPassesQuery()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProposalItemListResponseDto { Page = 2, Limit = 5, Total = 1 };
        var service = new FakeProposalService(
            itemListResult: ServiceResult<ProposalItemListResponseDto>.Success(
                response,
                "Proposal items retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetItems(proposalId, sceneId, page: 2, limit: 5);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProposalItemListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.ItemListQuery);
        Assert.Equal(sceneId, service.ItemListQuery.SceneId);
        Assert.Equal(2, service.ItemListQuery.Page);
        Assert.Equal(5, service.ItemListQuery.Limit);
    }

    [Fact]
    public async Task UpdateItem_ReturnsServiceResultAndPassesRequest()
    {
        var proposalItemId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new UpdateProposalItemRequestDto
        {
            Quantity = 6,
            CustomizationNote = "Increase quantity."
        };
        var response = new UpdateProposalItemResponseDto { ProposalItemId = proposalItemId };
        var service = new FakeProposalService(
            updateItemResult: ServiceResult<UpdateProposalItemResponseDto>.Success(
                response,
                "Proposal item updated successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.UpdateItem(proposalItemId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<UpdateProposalItemResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalItemId, service.ProposalItemId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateItemRequest);
    }

    [Fact]
    public async Task DeleteItem_ReturnsServiceResult()
    {
        var proposalItemId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new DeleteProposalItemResponseDto
        {
            ProposalItemId = proposalItemId,
            Deleted = true
        };
        var service = new FakeProposalService(
            deleteItemResult: ServiceResult<DeleteProposalItemResponseDto>.Success(
                response,
                "Proposal item deleted successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.DeleteItem(proposalItemId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<DeleteProposalItemResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalItemId, service.ProposalItemId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task SelectFinal_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new SelectFinalProposalRequestDto { Note = "Confirmed" };
        var response = new SelectFinalProposalResponseDto { ProposalId = proposalId };
        var service = new FakeProposalService(
            selectFinalResult: ServiceResult<SelectFinalProposalResponseDto>.Success(
                response,
                "Final proposal selected successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.SelectFinal(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<SelectFinalProposalResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.SelectFinalRequest);
    }

    [Fact]
    public async Task RequestRevision_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new RequestProposalRevisionRequestDto { RevisionNote = "Update layout." };
        var response = new RequestProposalRevisionResponseDto { ProposalId = proposalId };
        var service = new FakeProposalService(
            requestRevisionResult: ServiceResult<RequestProposalRevisionResponseDto>.Success(
                response,
                "Proposal revision requested successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.RequestRevision(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<RequestProposalRevisionResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.RequestRevisionRequest);
    }

    [Fact]
    public async Task Publish_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new PublishProposalRequestDto { Note = "Ready" };
        var response = new PublishProposalResponseDto { ProposalId = proposalId };
        var service = new FakeProposalService(
            publishResult: ServiceResult<PublishProposalResponseDto>.Success(
                response,
                "Proposal published for customer review successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.Publish(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<PublishProposalResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.PublishRequest);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultAndPassesRequest()
    {
        var proposalId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new UpdateProposalRequestDto { ProposalName = "Updated" };
        var response = new UpdateProposalResponseDto { ProposalId = proposalId };
        var service = new FakeProposalService(
            updateResult: ServiceResult<UpdateProposalResponseDto>.Success(
                response,
                "Proposal updated successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.Update(proposalId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<UpdateProposalResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(proposalId, service.ProposalId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task UpdateScene_ReturnsServiceResultAndPassesRequest()
    {
        var sceneId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new UpdateProposalSceneRequestDto { SceneName = "Updated scene" };
        var response = new UpdateProposalSceneResponseDto { SceneId = sceneId };
        var service = new FakeProposalService(
            updateSceneResult: ServiceResult<UpdateProposalSceneResponseDto>.Success(
                response,
                "Proposal scene updated successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.UpdateScene(sceneId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<UpdateProposalSceneResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(sceneId, service.SceneId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateSceneRequest);
    }

    [Fact]
    public async Task Actions_WithoutUserClaim_ReturnUnauthorized()
    {
        var service = new FakeProposalService();
        var controller = BuildController(service);

        Assert.IsType<UnauthorizedResult>(await controller.Create(Guid.NewGuid(), new CreateProposalRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.GetListByProject(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.CreateScene(Guid.NewGuid(), new CreateProposalSceneRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.GetScenes(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.GetSceneDetail(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.GetDetail(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.SyncItemsFromScene(Guid.NewGuid(), new SyncProposalItemsFromSceneRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.GetItems(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.UpdateItem(Guid.NewGuid(), new UpdateProposalItemRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.DeleteItem(Guid.NewGuid()));
        Assert.IsType<UnauthorizedResult>(await controller.SelectFinal(Guid.NewGuid(), new SelectFinalProposalRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.RequestRevision(Guid.NewGuid(), new RequestProposalRevisionRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.Publish(Guid.NewGuid(), new PublishProposalRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.Update(Guid.NewGuid(), new UpdateProposalRequestDto()));
        Assert.IsType<UnauthorizedResult>(await controller.UpdateScene(Guid.NewGuid(), new UpdateProposalSceneRequestDto()));
        Assert.Equal(0, service.CallCount);
    }

    private static ProposalsController BuildController(
        IProposalService service,
        Guid? currentUserId = null)
    {
        var claims = currentUserId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString()) }
            : [];

        return new ProposalsController(service)
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

    private sealed class FakeProposalService : IProposalService
    {
        private readonly ServiceResult<ProposalDto> _createResult;
        private readonly ServiceResult<ProposalListResponseDto> _listResult;
        private readonly ServiceResult<ProposalSceneDto> _sceneResult;
        private readonly ServiceResult<ProposalSceneListResponseDto> _sceneListResult;
        private readonly ServiceResult<ProposalSceneDetailDto> _sceneDetailResult;
        private readonly ServiceResult<ProposalDetailDto> _detailResult;
        private readonly ServiceResult<PublishedProposalDto> _publishedProposalResult;
        private readonly ServiceResult<SyncProposalItemsFromSceneResponseDto> _syncResult;
        private readonly ServiceResult<ProposalItemListResponseDto> _itemListResult;
        private readonly ServiceResult<UpdateProposalItemResponseDto> _updateItemResult;
        private readonly ServiceResult<DeleteProposalItemResponseDto> _deleteItemResult;
        private readonly ServiceResult<SelectFinalProposalResponseDto> _selectFinalResult;
        private readonly ServiceResult<RequestProposalRevisionResponseDto> _requestRevisionResult;
        private readonly ServiceResult<PublishProposalResponseDto> _publishResult;
        private readonly ServiceResult<UpdateProposalResponseDto> _updateResult;
        private readonly ServiceResult<UpdateProposalSceneResponseDto> _updateSceneResult;

        public FakeProposalService(
            ServiceResult<ProposalDto>? createResult = null,
            ServiceResult<ProposalListResponseDto>? listResult = null,
            ServiceResult<ProposalSceneDto>? sceneResult = null,
            ServiceResult<ProposalSceneListResponseDto>? sceneListResult = null,
            ServiceResult<ProposalSceneDetailDto>? sceneDetailResult = null,
            ServiceResult<ProposalDetailDto>? detailResult = null,
            ServiceResult<PublishedProposalDto>? publishedProposalResult = null,
            ServiceResult<SyncProposalItemsFromSceneResponseDto>? syncResult = null,
            ServiceResult<ProposalItemListResponseDto>? itemListResult = null,
            ServiceResult<UpdateProposalItemResponseDto>? updateItemResult = null,
            ServiceResult<DeleteProposalItemResponseDto>? deleteItemResult = null,
            ServiceResult<SelectFinalProposalResponseDto>? selectFinalResult = null,
            ServiceResult<RequestProposalRevisionResponseDto>? requestRevisionResult = null,
            ServiceResult<PublishProposalResponseDto>? publishResult = null,
            ServiceResult<UpdateProposalResponseDto>? updateResult = null,
            ServiceResult<UpdateProposalSceneResponseDto>? updateSceneResult = null)
        {
            _createResult = createResult ?? ServiceResult<ProposalDto>.Created(new ProposalDto());
            _listResult = listResult ?? ServiceResult<ProposalListResponseDto>.Success(new ProposalListResponseDto());
            _sceneResult = sceneResult ?? ServiceResult<ProposalSceneDto>.Created(new ProposalSceneDto());
            _sceneListResult = sceneListResult ?? ServiceResult<ProposalSceneListResponseDto>.Success(new ProposalSceneListResponseDto());
            _sceneDetailResult = sceneDetailResult ?? ServiceResult<ProposalSceneDetailDto>.Success(new ProposalSceneDetailDto());
            _detailResult = detailResult ?? ServiceResult<ProposalDetailDto>.Success(new ProposalDetailDto());
            _publishedProposalResult = publishedProposalResult ?? ServiceResult<PublishedProposalDto>.Success(new PublishedProposalDto());
            _syncResult = syncResult ?? ServiceResult<SyncProposalItemsFromSceneResponseDto>.Success(new SyncProposalItemsFromSceneResponseDto());
            _itemListResult = itemListResult ?? ServiceResult<ProposalItemListResponseDto>.Success(new ProposalItemListResponseDto());
            _updateItemResult = updateItemResult ?? ServiceResult<UpdateProposalItemResponseDto>.Success(new UpdateProposalItemResponseDto());
            _deleteItemResult = deleteItemResult ?? ServiceResult<DeleteProposalItemResponseDto>.Success(new DeleteProposalItemResponseDto());
            _selectFinalResult = selectFinalResult ?? ServiceResult<SelectFinalProposalResponseDto>.Success(new SelectFinalProposalResponseDto());
            _requestRevisionResult = requestRevisionResult ?? ServiceResult<RequestProposalRevisionResponseDto>.Success(new RequestProposalRevisionResponseDto());
            _publishResult = publishResult ?? ServiceResult<PublishProposalResponseDto>.Success(new PublishProposalResponseDto());
            _updateResult = updateResult ?? ServiceResult<UpdateProposalResponseDto>.Success(new UpdateProposalResponseDto());
            _updateSceneResult = updateSceneResult ?? ServiceResult<UpdateProposalSceneResponseDto>.Success(new UpdateProposalSceneResponseDto());
        }

        public int CallCount { get; private set; }
        public Guid ProjectId { get; private set; }
        public Guid ProposalId { get; private set; }
        public Guid SceneId { get; private set; }
        public Guid ProposalItemId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public CreateProposalRequestDto? CreateRequest { get; private set; }
        public ProposalListQueryDto? ListQuery { get; private set; }
        public CreateProposalSceneRequestDto? SceneRequest { get; private set; }
        public ProposalSceneListQueryDto? SceneListQuery { get; private set; }
        public SyncProposalItemsFromSceneRequestDto? SyncRequest { get; private set; }
        public ProposalItemListQueryDto? ItemListQuery { get; private set; }
        public UpdateProposalItemRequestDto? UpdateItemRequest { get; private set; }
        public SelectFinalProposalRequestDto? SelectFinalRequest { get; private set; }
        public RequestProposalRevisionRequestDto? RequestRevisionRequest { get; private set; }
        public PublishProposalRequestDto? PublishRequest { get; private set; }
        public UpdateProposalRequestDto? UpdateRequest { get; private set; }
        public UpdateProposalSceneRequestDto? UpdateSceneRequest { get; private set; }

        public Task<ServiceResult<ProposalDto>> CreateAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProposalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            CreateRequest = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<ProposalListResponseDto>> GetListByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            ProposalListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            ListQuery = query;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<ProposalSceneDto>> CreateSceneAsync(
            Guid proposalId,
            Guid currentUserId,
            CreateProposalSceneRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            SceneRequest = request;
            return Task.FromResult(_sceneResult);
        }

        public Task<ServiceResult<ProposalSceneListResponseDto>> GetScenesAsync(
            Guid proposalId,
            Guid currentUserId,
            ProposalSceneListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            SceneListQuery = query;
            return Task.FromResult(_sceneListResult);
        }

        public Task<ServiceResult<ProposalSceneDetailDto>> GetSceneDetailAsync(
            Guid sceneId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SceneId = sceneId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_sceneDetailResult);
        }

        public Task<ServiceResult<ProposalDetailDto>> GetDetailAsync(
            Guid proposalId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_detailResult);
        }

        public Task<ServiceResult<PublishedProposalDto>> GetPublishedByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_publishedProposalResult);
        }

        public Task<ServiceResult<ProposalItemListResponseDto>> GetItemsAsync(
            Guid proposalId,
            Guid currentUserId,
            ProposalItemListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            ItemListQuery = query;
            return Task.FromResult(_itemListResult);
        }

        public Task<ServiceResult<UpdateProposalItemResponseDto>> UpdateItemAsync(
            Guid proposalItemId,
            Guid currentUserId,
            UpdateProposalItemRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalItemId = proposalItemId;
            CurrentUserId = currentUserId;
            UpdateItemRequest = request;
            return Task.FromResult(_updateItemResult);
        }

        public Task<ServiceResult<DeleteProposalItemResponseDto>> DeleteItemAsync(
            Guid proposalItemId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalItemId = proposalItemId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_deleteItemResult);
        }

        public Task<ServiceResult<SyncProposalItemsFromSceneResponseDto>> SyncItemsFromSceneAsync(
            Guid proposalId,
            Guid currentUserId,
            SyncProposalItemsFromSceneRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            SyncRequest = request;
            return Task.FromResult(_syncResult);
        }

        public Task<ServiceResult<SelectFinalProposalResponseDto>> SelectFinalAsync(
            Guid proposalId,
            Guid currentUserId,
            SelectFinalProposalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            SelectFinalRequest = request;
            return Task.FromResult(_selectFinalResult);
        }

        public Task<ServiceResult<RequestProposalRevisionResponseDto>> RequestRevisionAsync(
            Guid proposalId,
            Guid currentUserId,
            RequestProposalRevisionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            RequestRevisionRequest = request;
            return Task.FromResult(_requestRevisionResult);
        }

        public Task<ServiceResult<PublishProposalResponseDto>> PublishAsync(
            Guid proposalId,
            Guid currentUserId,
            PublishProposalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            PublishRequest = request;
            return Task.FromResult(_publishResult);
        }

        public Task<ServiceResult<UpdateProposalResponseDto>> UpdateAsync(
            Guid proposalId,
            Guid currentUserId,
            UpdateProposalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposalId = proposalId;
            CurrentUserId = currentUserId;
            UpdateRequest = request;
            return Task.FromResult(_updateResult);
        }

        public Task<ServiceResult<UpdateProposalSceneResponseDto>> UpdateSceneAsync(
            Guid sceneId,
            Guid currentUserId,
            UpdateProposalSceneRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SceneId = sceneId;
            CurrentUserId = currentUserId;
            UpdateSceneRequest = request;
            return Task.FromResult(_updateSceneResult);
        }
    }
}
