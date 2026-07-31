#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Proposals;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;
using ApplicationRoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;

namespace FurniSpace.Application.Tests.Proposals;

public sealed class ProposalServiceTests
{
    [Fact]
    public async Task CreateAsync_WithAssignedDesignerAndConsultingProject_CreatesDraftProposal()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, assignedDesignerId: designerId),
            proposalCount: 2);
        var projects = new FakeProjectRepository("DESIGNER");
        var service = CreateService(repository, projects);

        var result = await service.CreateAsync(projectId, designerId, new CreateProposalRequestDto
        {
            ProposalName = " Cafe Proposal V3 ",
            Description = " Initial proposal "
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Proposal created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal("Cafe Proposal V3", result.Data.ProposalName);
        Assert.Equal("Initial proposal", result.Data.Description);
        Assert.Equal(3, result.Data.VersionNo);
        Assert.Equal(ProposalStatus.DRAFT, result.Data.Status);
        Assert.Single(repository.Proposals);
        Assert.Equal(designerId, repository.Proposals[0].CreatedBy);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithProjectWithoutDesigner_ReturnsDesignerNotAssigned()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService(new FakeProposalRepository(
            project: CreateProjectAccess(projectId, assignedDesignerId: null)));

        var result = await service.CreateAsync(projectId, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("DESIGNER_NOT_ASSIGNED", result.ErrorCode);
        Assert.Equal("Project must have an assigned designer before creating a proposal.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidProjectStatus_ReturnsInvalidProjectStatus()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        project.ProjectStatus = ProjectStatus.SPACE_VERIFIED;
        var service = CreateService(new FakeProposalRepository(project: project));

        var result = await service.CreateAsync(project.ProjectId, project.AssignedDesignerId!.Value, ValidCreateRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROJECT_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WithUnassignedDesigner_ReturnsForbidden()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        var repository = new FakeProposalRepository(project: project);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateAsync(project.ProjectId, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(403, result.Status);
        Assert.Empty(repository.Proposals);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidInput_ReturnsValidationErrors()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProposalRequestDto
        {
            ProposalName = " ",
            Description = new string('D', 1001)
        });

        Assert.Equal(400, result.Status);
        Assert.NotNull(result.Errors);
        Assert.Contains("Proposal name is required.", result.Errors);
        Assert.Contains("Proposal description must not exceed 1000 characters.", result.Errors);
    }

    [Fact]
    public async Task GetListByProjectAsync_WithCustomer_SetsCustomerVisibleFilter()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, customerId: customerId),
            listItems:
            [
                new ProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProjectId = projectId,
                    ProposalName = "Published",
                    Status = ProposalStatus.PUBLISHED
                }
            ]);
        var service = CreateService(repository, new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetListByProjectAsync(
            projectId,
            customerId,
            new ProposalListQueryDto { Status = ProposalStatus.PUBLISHED, Page = 2, Limit = 5 });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(5, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        Assert.NotNull(repository.LastListQuery);
        Assert.True(repository.LastListQuery.CustomerVisibleOnly);
        Assert.Equal(ProposalStatus.PUBLISHED, repository.LastListQuery.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_WithUnauthorizedUser_ReturnsForbidden()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedSalesId: Guid.NewGuid());
        var service = CreateService(
            new FakeProposalRepository(project: project),
            new FakeProjectRepository("SALES"));

        var result = await service.GetListByProjectAsync(
            project.ProjectId,
            Guid.NewGuid(),
            new ProposalListQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetListByProjectAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int limit,
        string expectedMessage)
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetListByProjectAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProposalListQueryDto { Page = page, Limit = limit });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task CreateSceneAsync_WithDraftProposal_CreatesActiveScene()
    {
        var proposalId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedDesignerId: designerId),
            sceneCount: 1);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateSceneAsync(proposalId, designerId, new CreateProposalSceneRequestDto
        {
            SceneName = " Main Layout ",
            SceneType = ProposalSceneType.THREE_D,
            MongoSceneId = " mongo-id ",
            PreviewFileId = Guid.NewGuid()
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Main Layout", result.Data.SceneName);
        Assert.Equal("mongo-id", result.Data.MongoSceneId);
        Assert.Equal(2, result.Data.VersionNo);
        Assert.True(result.Data.IsActive);
        Assert.Single(repository.Scenes);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateSceneAsync_WithPublishedProposal_ReturnsInvalidProposalStatus()
    {
        var context = CreateProposalContext(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        context.ProposalStatus = ProposalStatus.PUBLISHED;
        var service = CreateService(new FakeProposalRepository(context: context), new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateSceneAsync(
            context.ProposalId,
            context.AssignedDesignerId!.Value,
            ValidCreateSceneRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSceneAsync_WithMissingSceneFields_ReturnsValidationErrors()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.CreateSceneAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProposalSceneRequestDto());

        Assert.Equal(400, result.Status);
        Assert.NotNull(result.Errors);
        Assert.Contains("Scene name is required.", result.Errors);
        Assert.Contains("Scene type is required.", result.Errors);
    }

    [Fact]
    public async Task GetScenesAsync_WithAssignedDesigner_ReturnsFilteredScenes()
    {
        var designerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var scene = new ProposalSceneReadModel
        {
            SceneId = Guid.NewGuid(),
            ProposalId = proposalId,
            SceneName = "Main 3D layout",
            SceneType = ProposalSceneType.THREE_D,
            IsActive = true
        };
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedDesignerId: designerId),
            sceneItems: [scene]);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.GetScenesAsync(
            proposalId,
            designerId,
            new ProposalSceneListQueryDto
            {
                SceneType = ProposalSceneType.THREE_D,
                IsActive = true,
                Page = 2,
                Limit = 5
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(5, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        Assert.NotNull(repository.LastSceneListQuery);
        Assert.Equal(ProposalSceneType.THREE_D, repository.LastSceneListQuery.SceneType);
        Assert.True(repository.LastSceneListQuery.IsActive);
        Assert.False(repository.LastSceneListQuery.ActiveOnly);
    }

    [Fact]
    public async Task GetScenesAsync_WithCustomerDraftProposal_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var context = CreateProposalContext(Guid.NewGuid());
        context.CustomerId = customerId;
        context.ProposalStatus = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(context: context),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetScenesAsync(context.ProposalId, customerId, new ProposalSceneListQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetScenesAsync_WithCustomerPublishedProposal_ForcesActiveOnly()
    {
        var customerId = Guid.NewGuid();
        var context = CreateProposalContext(Guid.NewGuid());
        context.CustomerId = customerId;
        context.ProposalStatus = ProposalStatus.PUBLISHED;
        var repository = new FakeProposalRepository(
            context: context,
            sceneItems:
            [
                new ProposalSceneReadModel
                {
                    SceneId = Guid.NewGuid(),
                    ProposalId = context.ProposalId,
                    IsActive = true
                }
            ]);
        var service = CreateService(repository, new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetScenesAsync(
            context.ProposalId,
            customerId,
            new ProposalSceneListQueryDto { IsActive = false });

        Assert.Equal(200, result.Status);
        Assert.NotNull(repository.LastSceneListQuery);
        Assert.True(repository.LastSceneListQuery.ActiveOnly);
    }

    [Fact]
    public async Task GetSceneDetailAsync_WithAssignedSales_ReturnsSceneMetadata()
    {
        var salesId = Guid.NewGuid();
        var scene = CreateSceneDetail(Guid.NewGuid(), assignedSalesId: salesId);
        var service = CreateService(
            new FakeProposalRepository(sceneDetail: scene),
            new FakeProjectRepository("SALES"));

        var result = await service.GetSceneDetailAsync(scene.SceneId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(scene.SceneId, result.Data.SceneId);
        Assert.Equal(scene.ProjectId, result.Data.ProjectId);
        Assert.Equal(scene.PreviewFileUrl, result.Data.PreviewFileUrl);
    }

    [Fact]
    public async Task GetSceneDetailAsync_WithCustomerDraftProposal_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var scene = CreateSceneDetail(Guid.NewGuid(), customerId: customerId);
        scene.ProposalStatus = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(sceneDetail: scene),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetSceneDetailAsync(scene.SceneId, customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetSceneDetailAsync_WithMissingScene_ReturnsSceneNotFound()
    {
        var service = CreateService(new FakeProposalRepository(), new FakeProjectRepository("ADMIN"));

        var result = await service.GetSceneDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("SCENE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WithAssignedSales_ReturnsScenesAndItems()
    {
        var salesId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            detail: CreateDetail(proposalId, assignedSalesId: salesId));
        var service = CreateService(repository, new FakeProjectRepository("SALES"));

        var result = await service.GetDetailAsync(proposalId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(proposalId, result.Data.ProposalId);
        Assert.Single(result.Data.Scenes);
        Assert.Single(result.Data.Items);
        Assert.Equal("Proposal detail retrieved successfully.", result.Message);
    }

    [Fact]
    public async Task GetDetailAsync_WithCustomerDraftProposal_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var detail = CreateDetail(Guid.NewGuid(), customerId: customerId);
        detail.Status = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetDetailAsync(detail.ProposalId, customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WithMissingProposal_ReturnsProposalNotFound()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("PROPOSAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WithEmptyUser_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
    }

    [Fact]
    public async Task GetPublishedByProjectAsync_WithOwnerCustomer_ReturnsLatestPublishedProposal()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var proposal = CreateDetail(proposalId, customerId: customerId);
        proposal.ProjectId = projectId;
        proposal.Status = ProposalStatus.PUBLISHED;
        proposal.PublishedAt = DateTime.UtcNow;
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, customerId: customerId),
            publishedDetail: proposal);
        var service = CreateService(repository, new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetPublishedByProjectAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(proposalId, result.Data.ProposalId);
        Assert.Single(result.Data.Scenes);
        Assert.Equal($"/proposal-scenes/{proposal.Scenes[0].SceneId}/room-planner", result.Data.Scenes[0].RoomPlannerUrl);
        Assert.Single(result.Data.Items);
    }

    [Fact]
    public async Task GetPublishedByProjectAsync_WithDifferentCustomer_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService(
            new FakeProposalRepository(project: CreateProjectAccess(projectId, customerId: Guid.NewGuid())),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetPublishedByProjectAsync(projectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetPublishedByProjectAsync_WithoutPublishedProposal_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = CreateService(
            new FakeProposalRepository(project: CreateProjectAccess(projectId, customerId: customerId)),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetPublishedByProjectAsync(projectId, customerId);

        Assert.Equal(404, result.Status);
        Assert.Equal("PUBLISHED_PROPOSAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithDraftProposal_AddsProposalItems()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                SceneAreas = [CreateSceneAreaReadModel(Guid.NewGuid())],
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId));
        var beginCount = 0;
        var commitCount = 0;
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "chair-001",
                    ProductVersionId = productVersionId
                }
            ]
        };
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => { beginCount++; return Task.CompletedTask; },
            repository.SaveChangesAsync,
            _ => { commitCount++; return Task.CompletedTask; },
            _ => Task.CompletedTask);
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            unitOfWork,
            roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "chair-001",
                    ProductVersionId = productVersionId,
                    Quantity = 4,
                    CustomizationNote = "Use brown wood version."
                }
            ]
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Proposal items synced from Room Planner scene successfully.", result.Message);
        Assert.Single(repository.Items);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(1, result.Data.CreatedCount);
        Assert.Equal(0, result.Data.UpdatedCount);
        Assert.Equal(0, result.Data.RemovedCount);
        Assert.Equal("chair-001", repository.Items[0].SceneObjectId);
        Assert.Equal("chair-001", result.Data.Items[0].SceneObjectId);
        Assert.Equal("Cafe Chair", result.Data.Items[0].ProductNameSnapshot);
        Assert.Equal("Brown Wood", result.Data.Items[0].VersionNameSnapshot);
        Assert.Equal(4800000m, result.Data.Items[0].TotalPriceSnapshot);
        Assert.Equal(4800000m, result.Data.Items[0].SubtotalAmount);
        Assert.Equal(result.Data.Items[0].ProposalItemId, roomPlannerScenes.Scenes[sceneId].Objects[0].ProposalItemId);
        Assert.Equal(1, beginCount);
        Assert.Equal(1, commitCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithMissingProductVersion_ReturnsInvalidProductVersion()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = CreateRoomPlannerScene(sceneId, "chair-001");
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "chair-001",
                    ProductVersionId = Guid.NewGuid(),
                    Quantity = 1
                }
            ]
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PRODUCT_VERSION", result.ErrorCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithSceneOutsideProposal_ReturnsProposalSceneNotFound()
    {
        var proposalId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var service = CreateService(
            new FakeProposalRepository(context: CreateProposalContext(proposalId, assignedDesignerId: designerId)),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = Guid.NewGuid(),
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "chair-001",
                    ProductVersionId = Guid.NewGuid(),
                    Quantity = 1
                }
            ]
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("PROPOSAL_SCENE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithoutRoomPlannerScene_ReturnsRoomPlannerSceneNotFound()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: CreateSceneContext(proposalId, sceneId, context.ProjectId, designerId));
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            roomPlannerScenes: new FakeRoomPlannerSceneRepository());

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            designerId,
            CreateSyncRequest(sceneId, "chair-001", Guid.NewGuid()));

        Assert.Equal(404, result.Status);
        Assert.Equal("ROOM_PLANNER_SCENE_NOT_FOUND", result.ErrorCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithUnknownSceneObject_ReturnsSceneObjectNotFound()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: CreateSceneContext(proposalId, sceneId, context.ProjectId, designerId));
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = CreateRoomPlannerScene(sceneId, "chair-001", productVersionId);
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId));
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            designerId,
            CreateSyncRequest(sceneId, "missing-object", productVersionId));

        Assert.Equal(400, result.Status);
        Assert.Equal("SCENE_OBJECT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithExistingSceneObject_UpdatesProposalItem()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: CreateSceneContext(proposalId, sceneId, context.ProjectId, designerId));
        repository.Items.Add(new ProposalItem
        {
            ProposalItemId = existingItemId,
            ProposalId = proposalId,
            SceneId = sceneId,
            SceneObjectId = "chair-001",
            ProductVersionId = Guid.NewGuid(),
            ItemName = "Old Chair",
            Quantity = 1,
            UnitPriceSnapshot = 1m,
            TotalPriceSnapshot = 1m
        });
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = CreateRoomPlannerScene(sceneId, "chair-001", productVersionId);
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId, estimatedPrice: 200m));
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            designerId,
            CreateSyncRequest(sceneId, "chair-001", productVersionId, quantity: 3));

        Assert.Equal(200, result.Status);
        Assert.Single(repository.Items);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.CreatedCount);
        Assert.Equal(1, result.Data.UpdatedCount);
        Assert.Equal(existingItemId, result.Data.Items[0].ProposalItemId);
        Assert.Equal(600m, result.Data.Items[0].TotalPriceSnapshot);
        Assert.Equal(existingItemId, roomPlannerScenes.Scenes[sceneId].Objects[0].ProposalItemId);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithRevisionRequestedProposal_AddsProposalItems()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        context.ProposalStatus = ProposalStatus.REVISION_REQUESTED;
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: CreateSceneContext(proposalId, sceneId, context.ProjectId, designerId));
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = CreateRoomPlannerScene(sceneId, "chair-001", productVersionId);
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId));
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            designerId,
            CreateSyncRequest(sceneId, "chair-001", productVersionId));

        Assert.Equal(200, result.Status);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithInvalidQuantity_ReturnsInvalidQuantity()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.SyncItemsFromSceneAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateSyncRequest(Guid.NewGuid(), "chair-001", Guid.NewGuid(), quantity: 0));

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_QUANTITY", result.ErrorCode);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithAssignedSales_ReturnsForbidden()
    {
        var proposalId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedSalesId: salesId));
        var service = CreateService(repository, new FakeProjectRepository("SALES"));

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            salesId,
            CreateSyncRequest(Guid.NewGuid(), "chair-001", Guid.NewGuid()));

        Assert.Equal(403, result.Status);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithPublishedProposal_ReturnsProposalNotEditable()
    {
        var proposalId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        context.ProposalStatus = ProposalStatus.PUBLISHED;
        var service = CreateService(
            new FakeProposalRepository(context: context),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.SyncItemsFromSceneAsync(
            proposalId,
            designerId,
            CreateSyncRequest(Guid.NewGuid(), "chair-001", Guid.NewGuid()));

        Assert.Equal(400, result.Status);
        Assert.Equal("PROPOSAL_NOT_EDITABLE", result.ErrorCode);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithDuplicateSceneObjectIds_ConsolidatesQuantityAndLinksScene()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                SceneAreas = [CreateSceneAreaReadModel(Guid.NewGuid())],
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId));
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "product-004",
                    ProductVersionId = productVersionId
                }
            ]
        };
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            repository.SaveChangesAsync,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            unitOfWork,
            roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "product-004",
                    ProductVersionId = productVersionId,
                    Quantity = 2
                },
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "product-004",
                    ProductVersionId = productVersionId,
                    Quantity = 3
                }
            ]
        });

        Assert.Equal(200, result.Status);
        Assert.Single(repository.Items);
        Assert.Equal(5, repository.Items[0].Quantity);
        Assert.Equal(result.Data!.Items[0].ProposalItemId, roomPlannerScenes.Scenes[sceneId].Objects[0].ProposalItemId);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithDuplicateSceneObjectIdsAndDifferentProductVersions_ReturnsBadRequest()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var otherProductVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                SceneAreas = [CreateSceneAreaReadModel(Guid.NewGuid())],
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(CreateProductVersion(productVersionId));
        productVersions.ProductVersions.Add(CreateProductVersion(otherProductVersionId));
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "product-004",
                    ProductVersionId = productVersionId
                }
            ]
        };
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            repository.SaveChangesAsync,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            unitOfWork,
            roomPlannerScenes);

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "product-004",
                    ProductVersionId = productVersionId,
                    Quantity = 1
                },
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "product-004",
                    ProductVersionId = otherProductVersionId,
                    Quantity = 1
                }
            ]
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("DUPLICATE_SCENE_OBJECT_PRODUCT_VERSION", result.ErrorCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task GetItemsAsync_WithAssignedDesigner_ReturnsProposalItems()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedDesignerId: designerId),
            proposalItemList:
            [
                CreateProposalItemReadModel(proposalId, sceneId, proposalItemId)
            ]);
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "scene-object-001",
                    ProposalItemId = proposalItemId,
                    ProductVersionId = Guid.NewGuid()
                }
            ]
        };
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.GetItemsAsync(
            proposalId,
            designerId,
            new ProposalItemListQueryDto { SceneId = sceneId, Page = 2, Limit = 5 });

        Assert.Equal(200, result.Status);
        Assert.Equal("Proposal items retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("scene-object-001", result.Data.Items[0].SceneObjectId);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(5, result.Data.Limit);
        Assert.Equal(sceneId, repository.LastItemListQuery!.SceneId);
    }

    [Fact]
    public async Task GetItemsAsync_WithSingleUnlinkedMongoObject_ReturnsSceneObjectId()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedDesignerId: designerId),
            proposalItemList:
            [
                CreateProposalItemReadModel(proposalId, sceneId, proposalItemId)
            ]);
        var roomPlannerScenes = new FakeRoomPlannerSceneRepository();
        roomPlannerScenes.Scenes[sceneId] = new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "scene-object-001",
                    ProposalItemId = Guid.NewGuid(),
                    ProductVersionId = Guid.NewGuid()
                }
            ]
        };
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            roomPlannerScenes: roomPlannerScenes);

        var result = await service.GetItemsAsync(proposalId, designerId, new ProposalItemListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("scene-object-001", result.Data.Items[0].SceneObjectId);
    }

    [Fact]
    public async Task GetItemsAsync_WithCustomerDraftProposal_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId);
        context.CustomerId = customerId;
        context.ProposalStatus = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(context: context),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetItemsAsync(proposalId, customerId, new ProposalItemListQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetItemsAsync_WithMissingProposal_ReturnsProposalNotFound()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetItemsAsync(Guid.NewGuid(), Guid.NewGuid(), new ProposalItemListQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("PROPOSAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemAsync_WithDraftProposal_RecalculatesSubtotal()
    {
        var proposalId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var entity = CreateProposalItem(proposalItemId, proposalId);
        var detail = CreateProposalItemDetail(proposalItemId, proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(itemDetail: detail);
        repository.Items.Add(entity);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateItemAsync(
            proposalItemId,
            designerId,
            new UpdateProposalItemRequestDto
            {
                Quantity = 6,
                CustomizationNote = " Increase quantity. "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Proposal item updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data.Quantity);
        Assert.Equal(7200000m, result.Data.SubtotalAmount);
        Assert.Equal("Increase quantity.", result.Data.CustomizationNote);
        Assert.Equal(6, entity.Quantity);
        Assert.Equal(7200000m, entity.TotalPriceSnapshot);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateItemAsync_WithInvalidQuantity_ReturnsInvalidQuantity()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.UpdateItemAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateProposalItemRequestDto { Quantity = 0 });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_QUANTITY", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemAsync_WithPublishedProposal_ReturnsInvalidProposalStatus()
    {
        var itemId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var detail = CreateProposalItemDetail(itemId, Guid.NewGuid(), assignedSalesId: salesId);
        detail.ProposalStatus = ProposalStatus.PUBLISHED;
        var service = CreateService(
            new FakeProposalRepository(itemDetail: detail),
            new FakeProjectRepository("SALES"));

        var result = await service.UpdateItemAsync(
            itemId,
            salesId,
            new UpdateProposalItemRequestDto { Quantity = 2 });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemAsync_WithMissingItem_ReturnsProposalItemNotFound()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.UpdateItemAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateProposalItemRequestDto { Quantity = 2 });

        Assert.Equal(404, result.Status);
        Assert.Equal("PROPOSAL_ITEM_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task DeleteItemAsync_WithDraftProposal_RemovesProposalItem()
    {
        var proposalId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            itemDetail: CreateProposalItemDetail(proposalItemId, proposalId, assignedSalesId: salesId));
        repository.Items.Add(CreateProposalItem(proposalItemId, proposalId));
        var service = CreateService(repository, new FakeProjectRepository("SALES"));

        var result = await service.DeleteItemAsync(proposalItemId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Deleted);
        Assert.Equal(proposalItemId, result.Data.ProposalItemId);
        Assert.Empty(repository.Items);
        Assert.Equal(1, repository.RemoveItemCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteItemAsync_WithUnauthorizedDesigner_ReturnsForbidden()
    {
        var itemId = Guid.NewGuid();
        var service = CreateService(
            new FakeProposalRepository(itemDetail: CreateProposalItemDetail(itemId, Guid.NewGuid())),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.DeleteItemAsync(itemId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SelectFinalAsync_WithPublishedProposal_SelectsProposalAndRejectsOtherProposals()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId);
        detail.ProjectId = projectId;
        detail.Status = ProposalStatus.PUBLISHED;
        detail.AssignedSalesId = salesId;
        detail.AssignedDesignerId = designerId;
        var selectedProposal = new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Selected",
            Status = ProposalStatus.PUBLISHED
        };
        var otherProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalName = "Other",
            Status = ProposalStatus.PUBLISHED
        };
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeProposalRepository(detail: detail);
        repository.Proposals.AddRange([selectedProposal, otherProposal]);
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("CUSTOMER", project),
            notifications: dispatcher);

        var result = await service.SelectFinalAsync(proposalId, customerId, new SelectFinalProposalRequestDto
        {
            Note = "I confirm this as the final design proposal."
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProposalStatus.SELECTED, result.Data.ProposalStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, result.Data.ProjectStatus);
        Assert.Equal(ProposalStatus.SELECTED, selectedProposal.Status);
        Assert.Equal(ProposalStatus.REJECTED, otherProposal.Status);
        Assert.Equal(1, repository.RejectOtherActiveProposalsCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(NotificationType.ProposalFinalSelected, dispatcher.LastType);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal(proposalId, dispatcher.LastReferenceId);
        Assert.Contains(salesId, dispatcher.LastReceiverIds);
        Assert.Contains(designerId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task SelectFinalAsync_WithDifferentCustomer_ReturnsForbidden()
    {
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: Guid.NewGuid());
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.SelectFinalAsync(
            proposalId,
            Guid.NewGuid(),
            new SelectFinalProposalRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task SelectFinalAsync_WithPendingCustomizationRequest_ReturnsCustomizationRequestPending()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId);
        detail.ProjectId = projectId;
        detail.Status = ProposalStatus.PUBLISHED;
        var repository = new FakeProposalRepository(detail: detail);
        repository.Proposals.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Selected",
            Status = ProposalStatus.PUBLISHED
        });
        var service = CreateService(
            repository,
            new FakeProjectRepository("CUSTOMER", new Project
            {
                ProjectId = projectId,
                CustomerId = customerId
            }),
            customizationRequests: new FakeCustomizationRequestRepository(hasPending: true));

        var result = await service.SelectFinalAsync(
            proposalId,
            customerId,
            new SelectFinalProposalRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestPending, result.ErrorCode);
        Assert.Equal(0, repository.RejectOtherActiveProposalsCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SelectFinalAsync_WithAlreadySelectedProposal_ReturnsProposalAlreadySelected()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId);
        detail.Status = ProposalStatus.SELECTED;
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.SelectFinalAsync(
            proposalId,
            customerId,
            new SelectFinalProposalRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProposalAlreadySelected, result.ErrorCode);
    }

    [Fact]
    public async Task RequestRevisionAsync_WithPublishedOwner_UpdatesStatusAndNotifiesStaff()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId, assignedSalesId: salesId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(detail: detail);
        repository.Proposals.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = detail.ProjectId,
            ProposalName = detail.ProposalName,
            Status = ProposalStatus.PUBLISHED
        });
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("CUSTOMER"),
            notifications: dispatcher);

        var result = await service.RequestRevisionAsync(
            proposalId,
            customerId,
            new RequestProposalRevisionRequestDto { RevisionNote = " Please update layout. " });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProposalStatus.REVISION_REQUESTED, result.Data.ProposalStatus);
        Assert.Equal("Please update layout.", result.Data.RevisionNote);
        Assert.Equal(ProposalStatus.REVISION_REQUESTED, repository.Proposals[0].Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(NotificationType.ProposalRevisionRequested, dispatcher.LastType);
        Assert.Contains(salesId, dispatcher.LastReceiverIds);
        Assert.Contains(designerId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task RequestRevisionAsync_WithDraftProposal_ReturnsInvalidProposalStatus()
    {
        var customerId = Guid.NewGuid();
        var detail = CreateDetail(Guid.NewGuid(), customerId: customerId);
        detail.Status = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.RequestRevisionAsync(
            detail.ProposalId,
            customerId,
            new RequestProposalRevisionRequestDto { RevisionNote = "Update colors." });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task RequestRevisionAsync_WithDifferentCustomer_ReturnsForbidden()
    {
        var detail = CreateDetail(Guid.NewGuid(), customerId: Guid.NewGuid());
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.RequestRevisionAsync(
            detail.ProposalId,
            Guid.NewGuid(),
            new RequestProposalRevisionRequestDto { RevisionNote = "Update colors." });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task PublishAsync_WithDraftProposalAndActiveScene_PublishesWithoutChangingProjectStatus()
    {
        var designerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId, assignedDesignerId: designerId);
        detail.Status = ProposalStatus.DRAFT;
        var project = new Project
        {
            ProjectId = detail.ProjectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeProposalRepository(detail: detail) { HasActiveScene = true };
        repository.Proposals.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = detail.ProjectId,
            ProposalName = detail.ProposalName,
            Status = ProposalStatus.DRAFT
        });
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER", project),
            notifications: dispatcher);

        var result = await service.PublishAsync(proposalId, designerId, new PublishProposalRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProposalStatus.PUBLISHED, result.Data.ProposalStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, result.Data.ProjectStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
        Assert.Equal(ProposalStatus.PUBLISHED, repository.Proposals[0].Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(NotificationType.ProposalPublished, dispatcher.LastType);
        Assert.Contains(customerId, dispatcher.LastReceiverIds);
        Assert.Equal("PROPOSAL", dispatcher.LastReferenceType);
        Assert.Equal(proposalId, dispatcher.LastReferenceId);
    }

    [Fact]
    public async Task PublishAsync_DoesNotSendProjectStatusChangedNotification()
    {
        var designerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId, assignedDesignerId: designerId);
        detail.Status = ProposalStatus.DRAFT;
        var project = new Project
        {
            ProjectId = detail.ProjectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeProposalRepository(detail: detail) { HasActiveScene = true };
        repository.Proposals.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = detail.ProjectId,
            ProposalName = detail.ProposalName,
            Status = ProposalStatus.DRAFT
        });
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER", project),
            notifications: dispatcher);

        await service.PublishAsync(proposalId, designerId, new PublishProposalRequestDto());

        Assert.Equal(NotificationType.ProposalPublished, dispatcher.LastType);
        Assert.DoesNotContain(
            NotificationType.ProjectStatusChanged,
            dispatcher.DispatchedTypes);
    }

    [Fact]
    public async Task PublishAsync_WhenPublishingTwoProposals_SendsSeparateProposalPublishedNotifications()
    {
        var designerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var firstProposalId = Guid.NewGuid();
        var secondProposalId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeProposalRepository { HasActiveScene = true };
        repository.Proposals.AddRange(
        [
            new Proposal
            {
                ProposalId = firstProposalId,
                ProjectId = projectId,
                ProposalName = "Option A",
                Status = ProposalStatus.DRAFT
            },
            new Proposal
            {
                ProposalId = secondProposalId,
                ProjectId = projectId,
                ProposalName = "Option B",
                Status = ProposalStatus.DRAFT
            }
        ]);
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER", project),
            notifications: dispatcher);

        repository.DetailsByProposalId[firstProposalId] = CreateDetail(
            firstProposalId,
            customerId: customerId,
            assignedDesignerId: designerId,
            projectId: projectId);
        repository.DetailsByProposalId[firstProposalId].Status = ProposalStatus.DRAFT;
        repository.DetailsByProposalId[secondProposalId] = CreateDetail(
            secondProposalId,
            customerId: customerId,
            assignedDesignerId: designerId,
            projectId: projectId);
        repository.DetailsByProposalId[secondProposalId].Status = ProposalStatus.DRAFT;

        await service.PublishAsync(firstProposalId, designerId, new PublishProposalRequestDto());
        await service.PublishAsync(secondProposalId, designerId, new PublishProposalRequestDto());

        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Equal(2, dispatcher.DispatchedTypes.Count(t => t == NotificationType.ProposalPublished));
    }

    [Fact]
    public async Task CreateAsync_WithExistingPublishedProposal_AllowsCreatingAnotherDraft()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, assignedDesignerId: designerId),
            proposalCount: 1);
        repository.Proposals.Add(new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalName = "Published option",
            Status = ProposalStatus.PUBLISHED,
            VersionNo = 1
        });
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateAsync(projectId, designerId, ValidCreateRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(ProposalStatus.DRAFT, result.Data!.Status);
        Assert.Equal(2, result.Data.VersionNo);
    }

    [Fact]
    public async Task RequestRevisionAsync_KeepsProjectInProposalConsulting()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId);
        var project = new Project
        {
            ProjectId = detail.ProjectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeProposalRepository(detail: detail);
        repository.Proposals.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = detail.ProjectId,
            ProposalName = detail.ProposalName,
            Status = ProposalStatus.PUBLISHED
        });
        var service = CreateService(
            repository,
            new FakeProjectRepository("CUSTOMER", project));

        var result = await service.RequestRevisionAsync(
            proposalId,
            customerId,
            new RequestProposalRevisionRequestDto { RevisionNote = "Adjust layout." });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
    }

    [Fact]
    public async Task PublishAsync_WithoutActiveScene_ReturnsProposalSceneRequired()
    {
        var designerId = Guid.NewGuid();
        var detail = CreateDetail(Guid.NewGuid(), assignedDesignerId: designerId);
        detail.Status = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.PublishAsync(detail.ProposalId, designerId, new PublishProposalRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("PROPOSAL_SCENE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_WithDraftProposal_UpdatesMetadata()
    {
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(Guid.NewGuid(), assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(context: context);
        repository.Proposals.Add(new Proposal
        {
            ProposalId = context.ProposalId,
            ProjectId = context.ProjectId,
            ProposalName = "Old",
            Description = "Old description",
            VersionNo = 3,
            Status = ProposalStatus.DRAFT
        });
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateAsync(
            context.ProposalId,
            designerId,
            new UpdateProposalRequestDto { ProposalName = " Updated ", Description = " New description " });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Updated", result.Data.ProposalName);
        Assert.Equal("New description", result.Data.Description);
        Assert.Equal(3, result.Data.VersionNo);
        Assert.Equal("Updated", repository.Proposals[0].ProposalName);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithPublishedProposal_ReturnsInvalidProposalStatus()
    {
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(Guid.NewGuid(), assignedDesignerId: designerId);
        context.ProposalStatus = ProposalStatus.PUBLISHED;
        var service = CreateService(
            new FakeProposalRepository(context: context),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateAsync(
            context.ProposalId,
            designerId,
            new UpdateProposalRequestDto { ProposalName = "Updated" });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateSceneAsync_WithDraftProposal_UpdatesSqlMetadata()
    {
        var designerId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var previewFileId = Guid.NewGuid();
        var repository = new FakeProposalRepository(sceneContext: new ProposalSceneContextReadModel
        {
            SceneId = sceneId,
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalStatus = ProposalStatus.DRAFT,
            AssignedDesignerId = designerId
        });
        repository.Scenes.Add(new ProposalScene
        {
            SceneId = sceneId,
            ProposalId = proposalId,
            SceneName = "Old scene",
            SceneType = ProposalSceneType.THREE_D,
            MongoSceneId = "mongo-scene-id",
            IsActive = true
        });
        repository.ExistingFileIds.Add(previewFileId);
        repository.ProjectAreaProjectIds[projectAreaId] = projectId;
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateSceneAsync(
            sceneId,
            designerId,
            new UpdateProposalSceneRequestDto
            {
                SceneName = " Updated scene ",
                ProjectAreaId = projectAreaId,
                PreviewFileId = previewFileId,
                IsActive = false
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Updated scene", result.Data.SceneName);
        Assert.Equal(projectAreaId, result.Data.ProjectAreaId);
        Assert.Equal(previewFileId, result.Data.PreviewFileId);
        Assert.False(result.Data.IsActive);
        Assert.Equal("mongo-scene-id", result.Data.MongoSceneId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateSceneAsync_WithMissingFile_ReturnsFileNotFound()
    {
        var designerId = Guid.NewGuid();
        var sceneContext = new ProposalSceneContextReadModel
        {
            SceneId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProposalStatus = ProposalStatus.DRAFT,
            AssignedDesignerId = designerId
        };
        var service = CreateService(
            new FakeProposalRepository(sceneContext: sceneContext),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateSceneAsync(
            sceneContext.SceneId,
            designerId,
            new UpdateProposalSceneRequestDto { PreviewFileId = Guid.NewGuid() });

        Assert.Equal(404, result.Status);
        Assert.Equal("FILE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateSceneAsync_WithInvalidArea_ReturnsInvalidProjectArea()
    {
        var designerId = Guid.NewGuid();
        var previewFileId = Guid.NewGuid();
        var sceneContext = new ProposalSceneContextReadModel
        {
            SceneId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProposalStatus = ProposalStatus.DRAFT,
            AssignedDesignerId = designerId
        };
        var repository = new FakeProposalRepository(sceneContext: sceneContext);
        repository.ExistingFileIds.Add(previewFileId);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.UpdateSceneAsync(
            sceneContext.SceneId,
            designerId,
            new UpdateProposalSceneRequestDto
            {
                PreviewFileId = previewFileId,
                ProjectAreaId = Guid.NewGuid()
            });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROJECT_AREA", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateSceneAsync_WithMissingScene_ReturnsSceneNotFound()
    {
        var service = CreateService(
            new FakeProposalRepository(),
            new FakeProjectRepository("ADMIN"));

        var result = await service.UpdateSceneAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateProposalSceneRequestDto { SceneName = "Updated scene" });

        Assert.Equal(404, result.Status);
        Assert.Equal("SCENE_NOT_FOUND", result.ErrorCode);
    }

    private static ProposalService CreateService(
        FakeProposalRepository proposals,
        FakeProjectRepository? projects = null,
        FakeProductVersionRepository? productVersions = null,
        IUnitOfWork? unitOfWork = null,
        ApplicationRoomPlannerSceneRepository? roomPlannerScenes = null,
        INotificationDispatcher? notifications = null,
        ICustomizationRequestRepository? customizationRequests = null)
    {
        return new ProposalService(
            proposals,
            projects ?? new FakeProjectRepository("ADMIN"),
            productVersions ?? new FakeProductVersionRepository(),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(proposals.SaveChangesAsync),
            new ProposalServiceDependencies(
                roomPlannerScenes,
                notifications,
                customizationRequests: customizationRequests));
    }

    private static CreateProposalRequestDto ValidCreateRequest()
    {
        return new CreateProposalRequestDto
        {
            ProposalName = "Cafe Interior Design Proposal V1",
            Description = "Initial design proposal."
        };
    }

    private static CreateProposalSceneRequestDto ValidCreateSceneRequest()
    {
        return new CreateProposalSceneRequestDto
        {
            SceneName = "Main cafe layout",
            SceneType = ProposalSceneType.THREE_D
        };
    }

    private static SyncProposalItemsFromSceneRequestDto CreateSyncRequest(
        Guid sceneId,
        string sceneObjectId,
        Guid productVersionId,
        int quantity = 1)
    {
        return new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = sceneObjectId,
                    ProductVersionId = productVersionId,
                    Quantity = quantity
                }
            ]
        };
    }

    private static ProductVersionDetailReadModel CreateProductVersion(
        Guid productVersionId,
        decimal estimatedPrice = 1200000m)
    {
        return new ProductVersionDetailReadModel
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            ProductName = "Cafe Chair",
            VersionName = "Brown Wood",
            VersionType = ProductVersionType.STANDARD,
            EstimatedPrice = estimatedPrice,
            Status = ProductStatus.ACTIVE
        };
    }

    private static ProposalSceneContextReadModel CreateSceneContext(
        Guid proposalId,
        Guid sceneId,
        Guid projectId,
        Guid designerId)
    {
        return new ProposalSceneContextReadModel
        {
            ProposalId = proposalId,
            SceneId = sceneId,
            ProjectId = projectId,
            SceneAreas = [CreateSceneAreaReadModel(Guid.NewGuid())],
            ProposalStatus = ProposalStatus.DRAFT,
            AssignedDesignerId = designerId
        };
    }

    private static ProposalSceneAreaReadModel CreateSceneAreaReadModel(Guid projectAreaId) =>
        new()
        {
            ProposalSceneAreaId = Guid.NewGuid(),
            ProjectAreaId = projectAreaId,
            AreaName = "Main cafe area",
            SortOrder = 0
        };

    private static RoomPlannerSceneDocument CreateRoomPlannerScene(
        Guid sceneId,
        string sceneObjectId,
        Guid? productVersionId = null)
    {
        return new RoomPlannerSceneDocument
        {
            SqlSceneId = sceneId,
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = sceneObjectId,
                    ProductVersionId = productVersionId ?? Guid.Empty
                }
            ]
        };
    }

    private static ProposalProjectAccessReadModel CreateProjectAccess(
        Guid projectId,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalProjectAccessReadModel
        {
            ProjectId = projectId,
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProjectStatus = ProjectStatus.PROPOSAL_CONSULTING
        };
    }

    private static ProposalContextReadModel CreateProposalContext(
        Guid proposalId,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalContextReadModel
        {
            ProposalId = proposalId,
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalStatus = ProposalStatus.DRAFT
        };
    }

    private static ProposalDetailReadModel CreateDetail(
        Guid proposalId,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null,
        Guid? projectId = null)
    {
        return new ProposalDetailReadModel
        {
            ProposalId = proposalId,
            ProjectId = projectId ?? Guid.NewGuid(),
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalName = "Cafe Interior Design Proposal V1",
            Description = "Initial design proposal.",
            VersionNo = 1,
            Status = ProposalStatus.PUBLISHED,
            Scenes =
            [
                new ProposalSceneReadModel
                {
                    SceneId = Guid.NewGuid(),
                    ProposalId = proposalId,
                    SceneName = "Main layout",
                    SceneType = ProposalSceneType.THREE_D,
                    MongoSceneId = "mongo-id",
                    IsActive = true
                }
            ],
            Items =
            [
                new ProposalItemReadModel
                {
                    ProposalItemId = Guid.NewGuid(),
                    ProductNameSnapshot = "Cafe Chair",
                    Quantity = 4,
                    UnitPriceSnapshot = 1200000,
                    SubtotalAmount = 4800000
                }
            ]
        };
    }

    private static ProposalSceneDetailReadModel CreateSceneDetail(
        Guid sceneId,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalSceneDetailReadModel
        {
            SceneId = sceneId,
            ProposalId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalStatus = ProposalStatus.PUBLISHED,
            SceneName = "Main cafe layout",
            SceneType = ProposalSceneType.THREE_D,
            MongoSceneId = "mongo-scene-id",
            PreviewFileId = Guid.NewGuid(),
            PreviewFileUrl = "https://cdn.furnispace.test/preview.png",
            VersionNo = 1,
            IsActive = true
        };
    }

    private static ProposalItemReadModel CreateProposalItemReadModel(
        Guid proposalId,
        Guid sceneId,
        Guid? proposalItemId = null)
    {
        return new ProposalItemReadModel
        {
            ProposalItemId = proposalItemId ?? Guid.NewGuid(),
            ProposalId = proposalId,
            SceneId = sceneId,
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Cafe Chair",
            VersionNameSnapshot = "Brown Wood",
            MaterialSnapshot = "Wood",
            ColorSnapshot = "Brown",
            WidthSnapshot = 45,
            HeightSnapshot = 80,
            DepthSnapshot = 45,
            DimensionUnit = "cm",
            Quantity = 4,
            UnitPriceSnapshot = 1200000m,
            SubtotalAmount = 4800000m,
            CustomizationNote = "Use brown wood version."
        };
    }

    private static ProposalItemDetailReadModel CreateProposalItemDetail(
        Guid proposalItemId,
        Guid proposalId,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalItemDetailReadModel
        {
            ProposalItemId = proposalItemId,
            ProposalId = proposalId,
            SceneId = Guid.NewGuid(),
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Cafe Chair",
            VersionNameSnapshot = "Brown Wood",
            MaterialSnapshot = "Wood",
            ColorSnapshot = "Brown",
            WidthSnapshot = 45,
            HeightSnapshot = 80,
            DepthSnapshot = 45,
            DimensionUnit = "cm",
            Quantity = 4,
            UnitPriceSnapshot = 1200000m,
            SubtotalAmount = 4800000m,
            CustomizationNote = "Use brown wood version.",
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalStatus = ProposalStatus.DRAFT
        };
    }

    private static ProposalItem CreateProposalItem(Guid proposalItemId, Guid proposalId)
    {
        return new ProposalItem
        {
            ProposalItemId = proposalItemId,
            ProposalId = proposalId,
            SceneId = Guid.NewGuid(),
            ProductVersionId = Guid.NewGuid(),
            ItemName = "Cafe Chair",
            Material = "Wood",
            Color = "Brown",
            Width = 45,
            Height = 80,
            Depth = 45,
            Quantity = 4,
            UnitPriceSnapshot = 1200000m,
            TotalPriceSnapshot = 4800000m,
            Note = "Use brown wood version."
        };
    }

    private sealed class FakeCustomizationRequestRepository : ICustomizationRequestRepository
    {
        private readonly bool _hasPending;

        public FakeCustomizationRequestRepository(bool hasPending = false)
        {
            _hasPending = hasPending;
        }

        public Task<bool> HasPendingForProposalAsync(
            Guid proposalId,
            CancellationToken cancellationToken = default) => Task.FromResult(_hasPending);

        public Task<bool> HasActiveRequestForProductVersionAsync(
            Guid projectId,
            Guid proposalId,
            Guid productVersionId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<IReadOnlyList<CustomizationRequestReadModel>> GetByProjectAsync(
            CustomizationRequestQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomizationRequestReadModel>>([]);

        public Task<CustomizationRequestDetailReadModel?> GetDetailAsync(
            Guid customizationRequestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomizationRequestDetailReadModel?>(null);

        public Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(
            Guid proposalItemId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomizationSubmitContextReadModel?>(null);

        public Task<bool> HasQuotationForProposalAsync(
            Guid proposalId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> HasProductionVisibleRequestAsync(
            Guid projectId,
            Guid productionUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>> GetProductionQueueAsync(
            ProductionCustomizationRequestQueueQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>>([]);

        public Task<int> CountProductionQueueAsync(
            ProductionCustomizationRequestQueueQueryReadModel query,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public IQueryable<CustomizationRequest> Query() => Enumerable.Empty<CustomizationRequest>().AsQueryable();
        public Task<CustomizationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CustomizationRequest?>(null);
        public Task<IReadOnlyList<CustomizationRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomizationRequest>>([]);
        public Task AddAsync(CustomizationRequest entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<CustomizationRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(CustomizationRequest entity) { }
        public void Remove(CustomizationRequest entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeProposalRepository : IProposalRepository
    {
        private readonly ProposalProjectAccessReadModel? _project;
        private readonly ProposalContextReadModel? _context;
        private readonly ProposalDetailReadModel? _detail;
        private readonly IReadOnlyList<ProposalReadModel> _listItems;
        private readonly IReadOnlyList<ProposalSceneReadModel> _sceneItems;
        private readonly IReadOnlyList<ProposalItemReadModel> _proposalItemList;
        private readonly ProposalSceneDetailReadModel? _sceneDetail;
        private readonly ProposalItemDetailReadModel? _itemDetail;
        private readonly ProposalDetailReadModel? _publishedDetail;
        private readonly int _proposalCount;
        private readonly int _sceneCount;
        private readonly ProposalSceneContextReadModel? _sceneContext;

        public FakeProposalRepository(
            ProposalProjectAccessReadModel? project = null,
            ProposalContextReadModel? context = null,
            ProposalDetailReadModel? detail = null,
            IReadOnlyList<ProposalReadModel>? listItems = null,
            IReadOnlyList<ProposalSceneReadModel>? sceneItems = null,
            IReadOnlyList<ProposalItemReadModel>? proposalItemList = null,
            ProposalSceneDetailReadModel? sceneDetail = null,
            ProposalItemDetailReadModel? itemDetail = null,
            ProposalDetailReadModel? publishedDetail = null,
            int proposalCount = 0,
            int sceneCount = 0,
            ProposalSceneContextReadModel? sceneContext = null)
        {
            _project = project;
            _context = context;
            _detail = detail;
            _listItems = listItems ?? [];
            _sceneItems = sceneItems ?? [];
            _proposalItemList = proposalItemList ?? [];
            _sceneDetail = sceneDetail;
            _itemDetail = itemDetail;
            _publishedDetail = publishedDetail;
            _proposalCount = proposalCount;
            _sceneCount = sceneCount;
            _sceneContext = sceneContext;
        }

        public List<Proposal> Proposals { get; } = [];
        public List<ProposalScene> Scenes { get; } = [];
        public List<ProposalItem> Items { get; } = [];
        public HashSet<Guid> ExistingFileIds { get; } = [];
        public Dictionary<Guid, Guid> ProjectAreaProjectIds { get; } = [];
        public Dictionary<Guid, ProposalDetailReadModel> DetailsByProposalId { get; } = [];
        public bool HasActiveScene { get; set; }
        public int RejectOtherActiveProposalsCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public ProposalListQueryReadModel? LastListQuery { get; private set; }
        public ProposalSceneListQueryReadModel? LastSceneListQuery { get; private set; }
        public ProposalItemListQueryReadModel? LastItemListQuery { get; private set; }
        public int RemoveItemCallCount { get; private set; }

        public Task<ProposalProjectAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_project?.ProjectId == projectId ? _project : null);
        }

        public Task<ProposalContextReadModel?> GetProposalContextAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context?.ProposalId == proposalId ? _context : null);
        }

        public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_proposalCount);
        }

        public Task<IReadOnlyList<ProposalReadModel>> GetListAsync(
            ProposalListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(_listItems);
        }

        public Task<int> CountListAsync(ProposalListQueryReadModel query, CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(_listItems.Count);
        }

        public Task<int> CountScenesAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sceneCount);
        }

        public Task AddSceneAsync(ProposalScene scene, CancellationToken cancellationToken = default)
        {
            Scenes.Add(scene);
            return Task.CompletedTask;
        }

        public Task<ProposalDetailReadModel?> GetDetailAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            if (DetailsByProposalId.TryGetValue(proposalId, out var detailById))
            {
                return Task.FromResult<ProposalDetailReadModel?>(detailById);
            }

            return Task.FromResult(_detail?.ProposalId == proposalId ? _detail : null);
        }

        public Task<ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_publishedDetail?.ProjectId == projectId ? _publishedDetail : null);
        }

        public Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(
            ProposalSceneListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastSceneListQuery = query;
            return Task.FromResult(_sceneItems);
        }

        public Task<int> CountScenesAsync(
            ProposalSceneListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastSceneListQuery = query;
            return Task.FromResult(_sceneItems.Count);
        }

        public Task<ProposalSceneDetailReadModel?> GetSceneDetailAsync(Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sceneDetail?.SceneId == sceneId ? _sceneDetail : null);
        }

        public Task<ProposalSceneContextReadModel?> GetSceneContextAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _sceneContext?.ProposalId == proposalId && _sceneContext.SceneId == sceneId
                    ? _sceneContext
                    : null);
        }

        public Task<ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(
            Guid sceneId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sceneContext?.SceneId == sceneId ? _sceneContext : null);
        }

        public Task<ProposalScene?> GetSceneEntityAsync(Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Scenes.FirstOrDefault(scene => scene.SceneId == sceneId));
        }

        public Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProposalItem>>(
                Items.Where(item => item.ProposalId == proposalId && item.SceneId == sceneId).ToList());
        }

        public Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(
            ProposalItemListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastItemListQuery = query;
            return Task.FromResult(_proposalItemList);
        }

        public Task<int> CountItemsAsync(
            ProposalItemListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastItemListQuery = query;
            return Task.FromResult(_proposalItemList.Count);
        }

        public Task<ProposalItemDetailReadModel?> GetItemDetailAsync(
            Guid proposalItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_itemDetail?.ProposalItemId == proposalItemId ? _itemDetail : null);
        }

        public Task<ProposalItem?> GetItemEntityAsync(
            Guid proposalItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.ProposalItemId == proposalItemId));
        }

        public Task AddItemAsync(ProposalItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public void RemoveItem(ProposalItem item)
        {
            RemoveItemCallCount++;
            Items.Remove(item);
        }

        public Task<Proposal?> GetProposalEntityAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Proposals.FirstOrDefault(proposal => proposal.ProposalId == proposalId));
        }

        public Task RejectOtherActiveProposalsAsync(Guid projectId, Guid selectedProposalId, DateTime rejectedAt, CancellationToken cancellationToken = default)
        {
            RejectOtherActiveProposalsCallCount++;
            foreach (var proposal in Proposals.Where(proposal =>
                proposal.ProjectId == projectId &&
                proposal.ProposalId != selectedProposalId &&
                proposal.Status != ProposalStatus.ARCHIVED &&
                proposal.Status != ProposalStatus.REJECTED))
            {
                proposal.Status = ProposalStatus.REJECTED;
                proposal.RejectedAt = rejectedAt;
            }

            return Task.CompletedTask;
        }

        public Task<bool> HasProposalWithActiveSceneAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasSelectedFinalProposalAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasActiveSceneAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasActiveScene);
        }

        public Task<bool> FileExistsAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingFileIds.Contains(fileId));
        }

        public Task<bool> ProjectAreaBelongsToProjectAsync(
            Guid projectAreaId,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ProjectAreaProjectIds.TryGetValue(projectAreaId, out var storedProjectId) &&
                storedProjectId == projectId);
        }

        public IQueryable<Proposal> Query() => Proposals.AsQueryable();
        public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Proposals.FirstOrDefault(proposal => proposal.ProposalId == id));
        public Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Proposal>>(Proposals);
        public Task AddAsync(Proposal entity, CancellationToken cancellationToken = default)
        {
            Proposals.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<Proposal> entities, CancellationToken cancellationToken = default)
        {
            Proposals.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(Proposal entity) { }
        public void Remove(Proposal entity) => Proposals.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _roleName;
        private readonly Project? _project;

        public FakeProjectRepository(string? roleName, Project? project = null)
        {
            _roleName = roleName;
            _project = project;
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roleName);
        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_project?.ProjectId == id ? _project : null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeProductVersionRepository : IProductVersionRepository
    {
        public List<ProductVersionDetailReadModel> ProductVersions { get; } = [];

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductVersionDetailReadModel>>(
                ProductVersions
                    .Where(version => productVersionIds.Contains(version.ProductVersionId))
                    .ToList());
        }

        public IQueryable<ProductVersion> Query() => Array.Empty<ProductVersion>().AsQueryable();
        public Task<ProductVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersion?>(null);
        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductVersion>>([]);
        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersionDetailReadModel?>(null);
        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountProjectSpecificByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeRoomPlannerSceneRepository : ApplicationRoomPlannerSceneRepository
    {
        public Dictionary<Guid, RoomPlannerSceneDocument> Scenes { get; } = [];

        public Task<RoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RoomPlannerSceneDocument?>(null);
        }

        public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            Scenes.TryGetValue(sqlSceneId, out var scene);
            return Task.FromResult(scene);
        }

        public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            Scenes[document.SqlSceneId] = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Scenes.Remove(sqlSceneId));
        }
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? LastType { get; private set; }
        public IReadOnlyDictionary<string, string>? LastParameters { get; private set; }
        public List<Guid> LastReceiverIds { get; } = [];
        public Guid? LastProjectId { get; private set; }
        public string? LastReferenceType { get; private set; }
        public Guid? LastReferenceId { get; private set; }
        public int DispatchCount { get; private set; }
        public List<NotificationType> DispatchedTypes { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            DispatchedTypes.Add(type);
            LastType = type;
            LastParameters = parameters;
            LastReceiverIds.Clear();
            LastReceiverIds.AddRange(receiverIds);
            LastProjectId = projectId;
            LastReferenceType = referenceType;
            LastReferenceId = referenceId;
            return Task.CompletedTask;
        }
    }
}
