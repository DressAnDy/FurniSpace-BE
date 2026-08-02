#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.Services.CustomizationRequests;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class CustomizationRequestServiceTests
{
    public CustomizationRequestServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    #region GetByProjectAsync

    [Fact]
    public async Task GetByProjectAsync_CustomerOwnerGetsRequests()
    {
        var ids = CreateIds();
        var requestRepo = new FakeCustomizationRequestRepository
        {
            Items = [CreateRequest(ids)]
        };
        var service = CreateService(
            requestRepo,
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.CustomerId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetByProjectAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetByProjectAsync(Guid.NewGuid(), Guid.Empty, new CustomizationRequestQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_UnassignedUserReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, Guid.NewGuid(), new CustomizationRequestQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_MissingProjectReturnsProjectNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetByProjectAsync(Guid.NewGuid(), Guid.NewGuid(), new CustomizationRequestQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_ProductionWithoutVisibleVersionsReturnsForbidden()
    {
        var ids = CreateIds();
        var requestRepo = new FakeCustomizationRequestRepository
        {
            Items = [CreateRequest(ids, CustomizationStatus.SUBMITTED)],
            HasProductionVisibleRequest = false
        };
        var service = CreateService(
            requestRepo,
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.ProductionId, new CustomizationRequestQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_ProductionWithReviewingVersionGetsRequests()
    {
        var ids = CreateIds();
        var requestRepo = new FakeCustomizationRequestRepository
        {
            Items =
            [
                CreateRequest(ids, CustomizationStatus.REVIEWING),
                CreateRequest(ids, CustomizationStatus.SUBMITTED)
            ],
            HasProductionVisibleRequest = true
        };
        var service = CreateService(
            requestRepo,
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.ProductionId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task GetByProjectAsync_AdminGetsRequests()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository { Items = [CreateRequest(ids)] },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, Guid.NewGuid(), new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetByProjectAsync_AssignedDesignerGetsRequests()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository { Items = [CreateRequest(ids)] },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(DesignerRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.DesignerId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    #endregion

    #region GetProductionVersionQueueAsync

    [Fact]
    public async Task GetProductionVersionQueueAsync_ProductionUserGetsDefaultReviewingPendingQueue()
    {
        var ids = CreateIds();
        var queueItem = CreateVersionQueueItem(ids);
        var versionRepo = new FakeCustomizationRequestVersionRepository
        {
            VersionQueueItems = [queueItem]
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionQueueAsync(
            ids.ProductionId,
            new ProductionCustomizationVersionQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationVersionStatus.REVIEWING, result.Data.Items[0].Version.Status);
        Assert.Equal(ProductionFeasibilityStatus.PENDING, result.Data.Items[0].Version.FeasibilityStatus);
        Assert.Equal("Cafe Project", result.Data.Items[0].Project.ProjectName);
        Assert.Equal("Cafe Proposal", result.Data.Items[0].Proposal.ProposalName);
        Assert.Equal("Dining Chair", result.Data.Items[0].SourceProductVersion.VersionName);
    }

    [Fact]
    public async Task GetProductionVersionQueueAsync_CustomerReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetProductionVersionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationVersionQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionVersionQueueAsync_InvalidPaginationReturnsBadRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationVersionQueryDto { Page = 0 });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionVersionQueueAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionQueueAsync(
            Guid.Empty,
            new ProductionCustomizationVersionQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetProductionVersionQueueAsync_ProductionInvalidStatusReturnsBadRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationVersionQueryDto { Status = "SUBMITTED" });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionVersionQueueAsync_AdminFiltersByVersionStatus()
    {
        var ids = CreateIds();
        var versionRepo = new FakeCustomizationRequestVersionRepository
        {
            VersionQueueItems =
            [
                CreateVersionQueueItem(ids, CustomizationVersionStatus.REVIEWING, ProductionFeasibilityStatus.PENDING),
                CreateVersionQueueItem(ids, CustomizationVersionStatus.ACCEPTED, ProductionFeasibilityStatus.FEASIBLE)
            ]
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetProductionVersionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationVersionQueryDto { Status = "ACCEPTED" });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationVersionStatus.ACCEPTED, result.Data.Items[0].Version.Status);
    }

    #endregion

    #region GetDetailAsync

    [Fact]
    public async Task GetDetailAsync_AuthorizedUserGetsSnapshot()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeCustomizationRequestRepository { Detail = detail },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetDetailAsync(detail.CustomizationRequestId, ids.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(detail.SourceProductVersion.VersionName, result.Data!.SourceProductVersion!.VersionName);
    }

    [Fact]
    public async Task GetDetailAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    #endregion

    #region SubmitAsync

    [Fact]
    public async Task SubmitAsync_CustomerCreatesSubmittedRequestAndNotifies()
    {
        var ids = CreateIds();
        var dispatcher = new FakeNotificationDispatcher();
        var requestRepo = new FakeCustomizationRequestRepository
        {
            SubmitContext = CreateSubmitContext(ids),
            DetailFactory = entity => CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            requestRepo,
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole),
            dispatcher);

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(CustomizationStatus.SUBMITTED, requestRepo.AddedRequest!.Status);
        Assert.Equal(ids.CustomerId, requestRepo.AddedRequest.RequestedByCustomerId);
        Assert.Equal(ids.ProjectId, requestRepo.AddedRequest.ProjectId);
        Assert.Equal(1, requestRepo.SaveChangesCallCount);
        Assert.Equal(NotificationType.CustomizationRequestSubmitted, dispatcher.LastType);
        Assert.Contains(ids.CustomerId, dispatcher.LastReceivers);
        Assert.Contains(ids.SalesId, dispatcher.LastReceivers);
        Assert.Contains(ids.DesignerId, dispatcher.LastReceivers);
    }

    [Fact]
    public async Task SubmitAsync_InvalidRequestReturnsInvalidCustomizationRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.NewGuid(), new SubmitCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_ActiveRequestExistsReturnsBusinessError()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                SubmitContext = CreateSubmitContext(ids),
                HasActiveRequest = true
            },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ActiveCustomizationRequestAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.Empty, ValidSubmitRequest());

        Assert.Equal(401, result.Status);
    }

    #endregion

    #region CreateVersionAsync

    [Fact]
    public async Task CreateVersionAsync_DesignerCreatesDraftVersionSuccessfully()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId);
        var sourceVersion = CreateSourceProductVersion(ids);
        var productVersions = new FakeProductVersionRepository([sourceVersion]);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail
        };
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            requestRepo.SaveChangesAsync,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions,
            unitOfWork: unitOfWork);

        var result = await service.CreateVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            ValidCreateVersionRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(CustomizationVersionStatus.DRAFT, result.Data!.Version.Status);
        Assert.Equal(1, versionRepo.AddCallCount);
        Assert.Equal(1, productVersions.AddCallCount);
        Assert.Equal(ids.DesignerId, result.Data.Version.CreatedByDesignerId);
    }

    #endregion

    #region UpdateDraftVersionAsync

    [Fact]
    public async Task UpdateDraftVersionAsync_DesignerUpdatesDraftVersionSuccessfully()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId);
        var sourceVersion = CreateSourceProductVersion(ids);
        var productVersion = CreateCustomProductVersion(Guid.NewGuid());
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail
        };
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var productVersions = new FakeProductVersionRepository([sourceVersion, productVersion]);
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions);

        var result = await service.UpdateDraftVersionAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId,
            new UpdateCustomizationRequestVersionDto
            {
                VersionTitle = "Updated title",
                Material = "Teak",
                DimensionUnit = "cm"
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Updated title", version.VersionTitle);
        Assert.Equal("Teak", productVersion.Material);
        Assert.Equal(1, requestRepo.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateDraftVersionAsync_NonDraftVersionReturnsConflict()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        var productVersion = CreateCustomProductVersion(Guid.NewGuid());
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        version.Status = CustomizationVersionStatus.REVIEWING;
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.UpdateDraftVersionAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId,
            new UpdateCustomizationRequestVersionDto { VersionName = "Updated" });

        Assert.Equal(409, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationVersionNotDraft, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateDraftVersionAsync_InvalidDimensionUnitReturnsBadRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId);
        var productVersion = CreateCustomProductVersion(Guid.NewGuid());
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = detail
            },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository([CreateSourceProductVersion(ids), productVersion]));

        var result = await service.UpdateDraftVersionAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId,
            new UpdateCustomizationRequestVersionDto { DimensionUnit = "inch" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidDimensionUnit, result.ErrorCode);
    }

    #endregion

    #region SubmitVersionForReviewAsync

    [Fact]
    public async Task SubmitVersionForReviewAsync_MovesDraftToReviewingAndUpdatesRequestStatus()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId);
        var productVersion = CreateSourceProductVersion(ids);
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail
        };
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var productVersions = new FakeProductVersionRepository([productVersion]);
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole),
            productVersions: productVersions);

        var result = await service.SubmitVersionForReviewAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationVersionStatus.REVIEWING, version.Status);
        Assert.Equal(CustomizationStatus.REVIEWING, entity.Status);
        Assert.NotNull(version.SubmittedForReviewAt);
    }

    #endregion

    #region ReviewVersionAsync

    [Fact]
    public async Task ReviewVersionAsync_ProductionFeasibleReviewSuccess()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var productVersion = CreateSourceProductVersion(ids);
        var version = CreateReviewingVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        versionRepo.SetProductionDetail(version, entity, productVersion, ids);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING)
            },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole),
            productVersions: new FakeProductVersionRepository([productVersion]));

        var result = await service.ReviewVersionAsync(
            version.CustomizationRequestVersionId,
            ids.ProductionId,
            FeasibleVersionReview());

        Assert.Equal(200, result.Status);
        Assert.Equal(ProductionFeasibilityStatus.FEASIBLE, result.Data!.Version.FeasibilityStatus);
        Assert.Equal(CustomizationVersionStatus.REVIEWING, result.Data.Version.Status);
        Assert.Equal(ids.ProductionId, version.ProductionReviewedBy);
        Assert.True(version.MaterialAvailable);
    }

    [Fact]
    public async Task ReviewVersionAsync_SecondReviewReturnsConflict()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var productVersion = CreateSourceProductVersion(ids);
        var version = CreateReviewingVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        versionRepo.SetProductionDetail(version, entity, productVersion, ids);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING)
            },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole),
            productVersions: new FakeProductVersionRepository([productVersion]));

        var firstResult = await service.ReviewVersionAsync(
            version.CustomizationRequestVersionId,
            ids.ProductionId,
            FeasibleVersionReview());
        Assert.Equal(200, firstResult.Status);

        var secondResult = await service.ReviewVersionAsync(
            version.CustomizationRequestVersionId,
            ids.ProductionId,
            FeasibleVersionReview());

        Assert.Equal(409, secondResult.Status);
        Assert.Equal(
            CustomizationRequestErrorCodes.CustomizationVersionAlreadyReviewed,
            secondResult.ErrorCode);
    }

    #endregion

    #region GetVersionsAsync

    [Fact]
    public async Task GetVersionsAsync_DesignerSeesDraftAndSubmittedVersionsOrderedByVersionNo()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        var sourceProductVersion = CreateSourceProductVersion(ids);
        var draftVersion = CreateDraftVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 1);
        var reviewingVersion = CreateReviewingVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 2);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(draftVersion, CreateCustomProductVersion(draftVersion.ProductVersionId));
        versionRepo.StoreVersion(reviewingVersion, CreateCustomProductVersion(reviewingVersion.ProductVersionId));
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.GetVersionsAsync(entity.CustomizationRequestId, ids.DesignerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Equal(1, result.Data.Items[0].VersionNo);
        Assert.Equal(CustomizationVersionStatus.DRAFT, result.Data.Items[0].Status);
        Assert.Equal(2, result.Data.Items[1].VersionNo);
    }

    [Fact]
    public async Task GetVersionsAsync_ProductionDoesNotSeeDraftVersions()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        detail.AcceptedRequestVersionId = null;
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail,
            HasProductionVisibleRequest = true
        };
        var draftVersion = CreateDraftVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 1);
        var reviewingVersion = CreateReviewingVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 2);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(draftVersion, CreateCustomProductVersion(draftVersion.ProductVersionId));
        versionRepo.StoreVersion(reviewingVersion, CreateCustomProductVersion(reviewingVersion.ProductVersionId));
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetVersionsAsync(entity.CustomizationRequestId, ids.ProductionId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationVersionStatus.REVIEWING, result.Data.Items[0].Status);
    }

    [Fact]
    public async Task GetVersionsAsync_CustomerDoesNotSeeDraftVersions()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        var draftVersion = CreateDraftVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 1);
        var reviewingVersion = CreateReviewingVersion(ids, entity.CustomizationRequestId, Guid.NewGuid(), versionNo: 2);
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(draftVersion, CreateCustomProductVersion(draftVersion.ProductVersionId));
        versionRepo.StoreVersion(reviewingVersion, CreateCustomProductVersion(reviewingVersion.ProductVersionId));
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetVersionsAsync(entity.CustomizationRequestId, ids.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(2, result.Data.Items[0].VersionNo);
    }

    #endregion

    #region GetVersionDetailAsync

    [Fact]
    public async Task GetVersionDetailAsync_ReturnsAcceptedFlagForSelectedVersion()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.ACCEPTED);
        var productVersionId = Guid.NewGuid();
        var version = CreateFeasibleVersion(ids, entity.CustomizationRequestId, productVersionId);
        version.Status = CustomizationVersionStatus.ACCEPTED;
        entity.AcceptedRequestVersionId = version.CustomizationRequestVersionId;
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.ACCEPTED);
        detail.AcceptedRequestVersionId = version.CustomizationRequestVersionId;
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, CreateCustomProductVersion(productVersionId));
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetVersionDetailAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.IsAccepted);
    }

    [Fact]
    public async Task GetVersionDetailAsync_DraftVersionHiddenFromCustomerReturnsNotFound()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.SUBMITTED);
        var draftVersion = CreateDraftVersion(ids, entity.CustomizationRequestId, Guid.NewGuid());
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(draftVersion, CreateCustomProductVersion(draftVersion.ProductVersionId));
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetVersionDetailAsync(
            entity.CustomizationRequestId,
            draftVersion.CustomizationRequestVersionId,
            ids.CustomerId);

        Assert.Equal(404, result.Status);
        Assert.Equal(
            CustomizationRequestErrorCodes.CustomizationVersionNotFound,
            result.ErrorCode);
    }

    #endregion

    #region WithdrawVersionAsync

    [Fact]
    public async Task WithdrawVersionAsync_DesignerWithdrawsDraftVersion()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var detail = CreateDetail(ids, entity.CustomizationRequestId);
        var productVersion = CreateCustomProductVersion(Guid.NewGuid());
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail
        };
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole),
            productVersions: new FakeProductVersionRepository([productVersion]));

        var result = await service.WithdrawVersionAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationVersionStatus.WITHDRAWN, version.Status);
        Assert.NotNull(version.WithdrawnAt);
    }

    [Fact]
    public async Task WithdrawVersionAsync_ReviewedVersionReturnsConflict()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        var productVersion = CreateCustomProductVersion(Guid.NewGuid());
        var version = CreateDraftVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        version.Status = CustomizationVersionStatus.REVIEWING;
        version.FeasibilityStatus = ProductionFeasibilityStatus.FEASIBLE;
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var service = CreateService(
            new FakeCustomizationRequestRepository { ExistingEntity = entity, Detail = detail },
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.WithdrawVersionAsync(
            entity.CustomizationRequestId,
            version.CustomizationRequestVersionId,
            ids.DesignerId);

        Assert.Equal(409, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationVersionAlreadyReviewed, result.ErrorCode);
    }

    #endregion

    #region GetProductionVersionDetailAsync

    [Fact]
    public async Task GetProductionVersionDetailAsync_ProductionUserGetsDetail()
    {
        var ids = CreateIds();
        var queueItem = CreateVersionQueueItem(ids);
        var versionRepo = new FakeCustomizationRequestVersionRepository
        {
            VersionQueueItems = [queueItem]
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionDetailAsync(
            queueItem.Version.CustomizationRequestVersionId,
            ids.ProductionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(queueItem.Version.CustomizationRequestVersionId, result.Data!.Version.CustomizationRequestVersionId);
        Assert.Equal("Cafe Project", result.Data.Project.ProjectName);
    }

    [Fact]
    public async Task GetProductionVersionDetailAsync_CustomerReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetProductionVersionDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionVersionDetailAsync_MissingVersionReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionVersionDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationVersionNotFound, result.ErrorCode);
    }

    #endregion

    #region AcceptVersionAsync

    [Fact]
    public async Task AcceptVersionAsync_CustomerAcceptsFeasibleVersion()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.REVIEWING);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.REVIEWING);
        var productVersion = CreateSourceProductVersion(ids);
        var version = CreateFeasibleVersion(ids, entity.CustomizationRequestId, productVersion.ProductVersionId);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = detail
        };
        var versionRepo = new FakeCustomizationRequestVersionRepository();
        versionRepo.StoreVersion(version, productVersion);
        var service = CreateService(
            requestRepo,
            versionRepo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole),
            productVersions: new FakeProductVersionRepository([productVersion]));

        var result = await service.AcceptVersionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new AcceptCustomizationRequestDto
            {
                CustomizationRequestVersionId = version.CustomizationRequestVersionId
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.ACCEPTED, entity.Status);
        Assert.Equal(CustomizationVersionStatus.ACCEPTED, version.Status);
        Assert.Equal(version.CustomizationRequestVersionId, entity.AcceptedRequestVersionId);
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_CustomerCancelsSubmittedRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var requestRepo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            requestRepo,
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto { CancelReason = "No longer needed." });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.CANCELLED, entity.Status);
        Assert.Equal(1, requestRepo.UpdateCallCount);
        Assert.Equal(1, requestRepo.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledReturnsInvalidTransition()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.CANCELLED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.CANCELLED)
            },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationTransition, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_AcceptedRequestReturnsAlreadyAccepted()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.ACCEPTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.ACCEPTED)
            },
            new FakeCustomizationRequestVersionRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationAlreadyAccepted, result.ErrorCode);
    }

    #endregion

    #region Helpers

    private static CustomizationRequestService CreateService(
        FakeCustomizationRequestRepository customizationRequests,
        FakeCustomizationRequestVersionRepository customizationRequestVersions,
        FakeProposalRepository proposals,
        FakeProjectRepository projects,
        FakeNotificationDispatcher? dispatcher = null,
        FakeProductVersionRepository? productVersions = null,
        IUnitOfWork? unitOfWork = null,
        FakeCustomizationProjectFileRepository? projectFiles = null)
    {
        return new CustomizationRequestService(new CustomizationRequestServiceDependencies(
            customizationRequests,
            customizationRequestVersions,
            proposals,
            projects,
            productVersions ?? new FakeProductVersionRepository(),
            projectFiles ?? new FakeCustomizationProjectFileRepository(),
            dispatcher ?? new FakeNotificationDispatcher(),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(customizationRequests.SaveChangesAsync)));
    }

    private static TestIds CreateIds() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid());

    private static ProposalProjectAccessReadModel CreateProject(TestIds ids) => new()
    {
        ProjectId = ids.ProjectId,
        CustomerId = ids.CustomerId,
        AssignedSalesId = ids.SalesId,
        AssignedDesignerId = ids.DesignerId,
        ProjectStatus = ProjectStatus.PROPOSAL_CONSULTING
    };

    private static Project CreateProjectEntity(TestIds ids) => new()
    {
        ProjectId = ids.ProjectId,
        ProjectCode = "PRJ-000001",
        CustomerId = ids.CustomerId
    };

    private static CustomizationRequestReadModel CreateRequest(
        TestIds ids,
        CustomizationStatus status = CustomizationStatus.SUBMITTED) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = ids.ProjectId,
        ProposalId = ids.ProposalId,
        SourceProductVersionId = ids.ProductVersionId,
        CustomerId = ids.CustomerId,
        AssignedSalesId = ids.SalesId,
        AssignedDesignerId = ids.DesignerId,
        ProjectName = "Cafe",
        RequestTitle = "Change chair material",
        Status = status
    };

    private static ProductionCustomizationVersionQueueReadModel CreateVersionQueueItem(
        TestIds ids,
        CustomizationVersionStatus versionStatus = CustomizationVersionStatus.REVIEWING,
        ProductionFeasibilityStatus feasibilityStatus = ProductionFeasibilityStatus.PENDING) => new()
    {
        Request = new CustomizationRequestReadModel
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            ProposalId = ids.ProposalId,
            SourceProductVersionId = ids.ProductVersionId,
            RequestTitle = "Change chair material",
            Status = CustomizationStatus.REVIEWING,
            ProjectName = "Cafe Project",
            CustomerId = ids.CustomerId,
            AssignedSalesId = ids.SalesId,
            AssignedDesignerId = ids.DesignerId,
            UpdatedAt = DateTime.UtcNow
        },
        Version = new CustomizationRequestVersionReadModel
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            VersionNo = 1,
            Status = versionStatus,
            FeasibilityStatus = feasibilityStatus,
            CreatedByDesignerId = ids.DesignerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ProductVersion = new ProductVersion
            {
                ProductVersionId = Guid.NewGuid(),
                VersionName = "Custom Chair"
            }
        },
        ProposalName = "Cafe Proposal",
        ProposalStatus = ProposalStatus.PUBLISHED,
        SourceProductVersion = new ProductVersion
        {
            ProductVersionId = ids.ProductVersionId,
            ProductId = Guid.NewGuid(),
            VersionName = "Dining Chair"
        }
    };

    private static CustomizationRequestDetailReadModel CreateDetail(
        TestIds ids,
        Guid? customizationRequestId = null,
        CustomizationStatus status = CustomizationStatus.SUBMITTED)
    {
        var request = CreateRequest(ids, status);
        return new CustomizationRequestDetailReadModel
        {
            CustomizationRequestId = customizationRequestId ?? request.CustomizationRequestId,
            ProjectId = request.ProjectId,
            ProposalId = request.ProposalId,
            SourceProductVersionId = request.SourceProductVersionId,
            CustomerId = request.CustomerId,
            AssignedSalesId = request.AssignedSalesId,
            AssignedDesignerId = request.AssignedDesignerId,
            RequestTitle = request.RequestTitle,
            Status = status,
            ProjectName = request.ProjectName,
            SourceProductVersion = new ProductVersion
            {
                ProductVersionId = request.SourceProductVersionId,
                ProductId = Guid.NewGuid(),
                VersionName = "Chair",
                Material = "Oak"
            }
        };
    }

    private static CustomizationRequest CreateEntity(
        TestIds ids,
        CustomizationStatus status) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = ids.ProjectId,
        ProposalId = ids.ProposalId,
        SourceProductVersionId = ids.ProductVersionId,
        RequestedByCustomerId = ids.CustomerId,
        RequestTitle = "Change chair material",
        RequestedMaterial = "Dark oak wood",
        Status = status
    };

    private static ProductVersion CreateSourceProductVersion(TestIds ids) => new()
    {
        ProductVersionId = ids.ProductVersionId,
        ProductId = Guid.NewGuid(),
        ProjectId = ids.ProjectId,
        VersionCode = "PV-BASE-001",
        VersionName = "Dining Chair",
        VersionType = ProductVersionType.STANDARD,
        Material = "Oak",
        Color = "Natural",
        EstimatedPrice = 1000000m,
        DimensionUnit = "cm",
        IsDefault = true,
        IsPublic = true,
        IsProjectSpecific = false,
        Status = ProductStatus.ACTIVE
    };

    private static CustomizationRequestVersion CreateDraftVersion(
        TestIds ids,
        Guid customizationRequestId,
        Guid productVersionId,
        int versionNo = 1) => new()
    {
        CustomizationRequestVersionId = Guid.NewGuid(),
        CustomizationRequestId = customizationRequestId,
        ProductVersionId = productVersionId,
        VersionNo = versionNo,
        CreatedByDesignerId = ids.DesignerId,
        Status = CustomizationVersionStatus.DRAFT,
        FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CustomizationRequestVersion CreateReviewingVersion(
        TestIds ids,
        Guid customizationRequestId,
        Guid productVersionId,
        int versionNo = 1) => new()
    {
        CustomizationRequestVersionId = Guid.NewGuid(),
        CustomizationRequestId = customizationRequestId,
        ProductVersionId = productVersionId,
        VersionNo = versionNo,
        CreatedByDesignerId = ids.DesignerId,
        Status = CustomizationVersionStatus.REVIEWING,
        FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
        SubmittedForReviewAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ProductVersion CreateCustomProductVersion(Guid productVersionId) => new()
    {
        ProductVersionId = productVersionId,
        ProductId = Guid.NewGuid(),
        VersionCode = "CUSTOM-001",
        VersionName = "Custom Alternative",
        VersionType = ProductVersionType.PROJECT_SPECIFIC,
        Material = "Oak",
        IsProjectSpecific = true,
        IsPublic = false,
        Status = ProductStatus.ACTIVE
    };

    private static CustomizationRequestVersion CreateFeasibleVersion(
        TestIds ids,
        Guid customizationRequestId,
        Guid productVersionId) => new()
    {
        CustomizationRequestVersionId = Guid.NewGuid(),
        CustomizationRequestId = customizationRequestId,
        ProductVersionId = productVersionId,
        VersionNo = 1,
        CreatedByDesignerId = ids.DesignerId,
        Status = CustomizationVersionStatus.REVIEWING,
        FeasibilityStatus = ProductionFeasibilityStatus.FEASIBLE,
        MaterialAvailable = true,
        EstimatedProductionDays = 5,
        EstimatedAdditionalCost = 1500000m,
        ProductionReviewedBy = ids.ProductionId,
        ProductionReviewedAt = DateTime.UtcNow,
        SubmittedForReviewAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CustomizationSubmitContextReadModel CreateSubmitContext(TestIds ids) => new()
    {
        ProposalItemId = ids.ProposalItemId,
        ProductVersionId = ids.ProductVersionId,
        ProposalId = ids.ProposalId,
        ProjectId = ids.ProjectId,
        CustomerId = ids.CustomerId,
        AssignedSalesId = ids.SalesId,
        AssignedDesignerId = ids.DesignerId,
        ProposalStatus = ProposalStatus.PUBLISHED,
        ProjectStatus = ProjectStatus.PROPOSAL_CONSULTING,
        ProjectName = "Cafe",
        ProposalName = "Cafe Proposal"
    };

    private static SubmitCustomizationRequestDto ValidSubmitRequest() => new()
    {
        RequestTitle = "Change chair material",
        RequestedMaterial = "Dark oak wood",
        RequestedColor = "Dark brown"
    };

    private static CreateCustomizationRequestVersionDto ValidCreateVersionRequest() => new()
    {
        VersionName = "Custom Chair V1",
        DimensionUnit = "cm",
        Material = "Dark oak wood"
    };

    private static ReviewCustomizationVersionDto FeasibleVersionReview() => new()
    {
        Result = "FEASIBLE",
        MaterialAvailable = true,
        EstimatedProductionDays = 5,
        EstimatedAdditionalCost = 1500000,
        AdditionalCostReason = "Custom finishing requires extra cost.",
        FeasibilityNote = "Material is available.",
        ProductionRiskNote = "Adds five days."
    };

    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string SalesRole = "SALES";
    private const string DesignerRole = "DESIGNER";
    private const string ProductionRole = "PRODUCTION";

    private sealed record TestIds(
        Guid ProjectId,
        Guid ProposalId,
        Guid ProposalItemId,
        Guid ProductVersionId,
        Guid CustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid ProductionId);

    #endregion

    #region Fakes

    private sealed class FakeCustomizationRequestRepository : ICustomizationRequestRepository
    {
        public IReadOnlyList<CustomizationRequestReadModel> Items { get; init; } = [];
        public CustomizationRequestDetailReadModel? Detail { get; init; }
        public Func<CustomizationRequest, CustomizationRequestDetailReadModel>? DetailFactory { get; init; }
        public CustomizationSubmitContextReadModel? SubmitContext { get; init; }
        public bool HasQuotation { get; init; }
        public bool HasActiveRequest { get; init; }
        public bool HasProductionVisibleRequest { get; init; }
        public CustomizationRequest? ExistingEntity { get; init; }
        public CustomizationRequest? AddedRequest { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public Task<IReadOnlyList<CustomizationRequestReadModel>> GetByProjectAsync(
            CustomizationRequestQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var result = Items
                .Where(item => item.ProjectId == query.ProjectId)
                .Where(item => !query.ProposalId.HasValue || item.ProposalId == query.ProposalId)
                .Where(item => !query.SourceProductVersionId.HasValue || item.SourceProductVersionId == query.SourceProductVersionId)
                .Where(item => !query.Status.HasValue || item.Status == query.Status)
                .ToList();
            return Task.FromResult<IReadOnlyList<CustomizationRequestReadModel>>(result);
        }

        public Task<CustomizationRequestDetailReadModel?> GetDetailAsync(
            Guid customizationRequestId,
            CancellationToken cancellationToken = default)
        {
            var detail = DetailFactory is not null && AddedRequest is not null
                ? DetailFactory(AddedRequest)
                : Detail;

            if (detail?.CustomizationRequestId != customizationRequestId)
            {
                return Task.FromResult<CustomizationRequestDetailReadModel?>(null);
            }

            if (ExistingEntity?.CustomizationRequestId == customizationRequestId)
            {
                SyncEntityToDetail(ExistingEntity, detail);
            }

            return Task.FromResult<CustomizationRequestDetailReadModel?>(detail);
        }

        public Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(
            Guid proposalItemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SubmitContext?.ProposalItemId == proposalItemId ? SubmitContext : null);

        public Task<bool> HasQuotationForProposalAsync(
            Guid proposalId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasQuotation);

        public Task<bool> HasProductionVisibleRequestAsync(
            Guid projectId,
            Guid productionUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasProductionVisibleRequest);

        public Task<bool> HasPendingForProposalAsync(
            Guid proposalId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasActiveRequestForProductVersionAsync(
            Guid projectId,
            Guid proposalId,
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasActiveRequest);

        public Task AddAsync(CustomizationRequest entity, CancellationToken cancellationToken = default)
        {
            AddedRequest = entity;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public IQueryable<CustomizationRequest> Query() => Enumerable.Empty<CustomizationRequest>().AsQueryable();
        public Task<CustomizationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingEntity?.CustomizationRequestId == id ? ExistingEntity : null);
        public Task<IReadOnlyList<CustomizationRequest>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomizationRequest>>([]);
        public Task AddRangeAsync(IEnumerable<CustomizationRequest> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(CustomizationRequest entity) => UpdateCallCount++;
        public void Remove(CustomizationRequest entity) { }

        private static void SyncEntityToDetail(
            CustomizationRequest entity,
            CustomizationRequestDetailReadModel detail)
        {
            detail.Status = entity.Status;
            detail.AcceptedRequestVersionId = entity.AcceptedRequestVersionId;
            detail.UpdatedAt = entity.UpdatedAt;
        }
    }

    private sealed class FakeCustomizationRequestVersionRepository : ICustomizationRequestVersionRepository
    {
        private readonly Dictionary<Guid, CustomizationRequestVersion> _versions = new();
        private readonly Dictionary<Guid, ProductVersion> _productVersions = new();
        private readonly Dictionary<Guid, ProductionCustomizationVersionDetailReadModel> _productionDetails = new();

        public IReadOnlyList<ProductionCustomizationVersionQueueReadModel> VersionQueueItems { get; init; } = [];
        public int AddCallCount { get; private set; }

        public void StoreVersion(CustomizationRequestVersion version, ProductVersion productVersion)
        {
            _versions[version.CustomizationRequestVersionId] = version;
            _productVersions[productVersion.ProductVersionId] = productVersion;
        }

        public void SetProductionDetail(
            CustomizationRequestVersion version,
            CustomizationRequest request,
            ProductVersion sourceProductVersion,
            TestIds ids)
        {
            _productionDetails[version.CustomizationRequestVersionId] = new ProductionCustomizationVersionDetailReadModel
            {
                Version = ToReadModel(version, sourceProductVersion),
                Request = new CustomizationRequestReadModel
                {
                    CustomizationRequestId = request.CustomizationRequestId,
                    ProjectId = request.ProjectId,
                    ProposalId = request.ProposalId,
                    SourceProductVersionId = request.SourceProductVersionId,
                    RequestTitle = request.RequestTitle,
                    Status = request.Status,
                    ProjectName = "Cafe Project",
                    CustomerId = ids.CustomerId,
                    AssignedSalesId = ids.SalesId,
                    AssignedDesignerId = ids.DesignerId
                },
                ProposalName = "Cafe Proposal",
                ProposalStatus = ProposalStatus.PUBLISHED,
                SourceProductVersion = sourceProductVersion
            };
        }

        public Task<CustomizationRequestVersion?> GetByIdForUpdateAsync(
            Guid customizationRequestVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.TryGetValue(customizationRequestVersionId, out var version) ? version : null);

        public Task<CustomizationRequestVersion?> GetByIdWithRequestAsync(
            Guid customizationRequestVersionId,
            CancellationToken cancellationToken = default)
        {
            if (!_versions.TryGetValue(customizationRequestVersionId, out var version))
            {
                return Task.FromResult<CustomizationRequestVersion?>(null);
            }

            if (_productVersions.TryGetValue(version.ProductVersionId, out var productVersion))
            {
                version.ProductVersion = productVersion;
            }

            return Task.FromResult<CustomizationRequestVersion?>(version);
        }

        public Task<int> GetNextVersionNoAsync(
            Guid customizationRequestId,
            CancellationToken cancellationToken = default)
        {
            var next = _versions.Values
                .Where(version => version.CustomizationRequestId == customizationRequestId)
                .Select(version => version.VersionNo)
                .DefaultIfEmpty(0)
                .Max() + 1;
            return Task.FromResult(next);
        }

        public Task<IReadOnlyList<CustomizationRequestVersionReadModel>> GetByRequestIdAsync(
            Guid customizationRequestId,
            CancellationToken cancellationToken = default)
        {
            var items = _versions.Values
                .Where(version => version.CustomizationRequestId == customizationRequestId)
                .Select(version =>
                {
                    _productVersions.TryGetValue(version.ProductVersionId, out var productVersion);
                    return ToReadModel(version, productVersion ?? new ProductVersion { ProductVersionId = version.ProductVersionId });
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<CustomizationRequestVersionReadModel>>(items);
        }

        public Task<IReadOnlyList<ProductionCustomizationVersionQueueReadModel>> GetProductionQueueAsync(
            ProductionCustomizationVersionQueueQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var result = VersionQueueItems.AsEnumerable();

            if (query.Statuses is { Count: > 0 })
            {
                result = result.Where(item => query.Statuses.Contains(item.Version.Status));
            }

            if (query.FeasibilityStatuses is { Count: > 0 })
            {
                result = result.Where(item => query.FeasibilityStatuses.Contains(item.Version.FeasibilityStatus));
            }

            if (query.ProjectId.HasValue)
            {
                result = result.Where(item => item.Request.ProjectId == query.ProjectId.Value);
            }

            if (query.ProposalId.HasValue)
            {
                result = result.Where(item => item.Request.ProposalId == query.ProposalId.Value);
            }

            if (query.MaterialAvailable.HasValue)
            {
                result = result.Where(item => item.Version.MaterialAvailable == query.MaterialAvailable.Value);
            }

            var items = result
                .OrderByDescending(item => item.Version.UpdatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProductionCustomizationVersionQueueReadModel>>(items);
        }

        public Task<int> CountProductionQueueAsync(
            ProductionCustomizationVersionQueueQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var result = VersionQueueItems.AsEnumerable();

            if (query.Statuses is { Count: > 0 })
            {
                result = result.Where(item => query.Statuses.Contains(item.Version.Status));
            }

            if (query.FeasibilityStatuses is { Count: > 0 })
            {
                result = result.Where(item => query.FeasibilityStatuses.Contains(item.Version.FeasibilityStatus));
            }

            if (query.ProjectId.HasValue)
            {
                result = result.Where(item => item.Request.ProjectId == query.ProjectId.Value);
            }

            if (query.ProposalId.HasValue)
            {
                result = result.Where(item => item.Request.ProposalId == query.ProposalId.Value);
            }

            if (query.MaterialAvailable.HasValue)
            {
                result = result.Where(item => item.Version.MaterialAvailable == query.MaterialAvailable.Value);
            }

            return Task.FromResult(result.Count());
        }

        public Task<ProductionCustomizationVersionDetailReadModel?> GetProductionDetailAsync(
            Guid customizationRequestVersionId,
            CancellationToken cancellationToken = default)
        {
            if (_productionDetails.TryGetValue(customizationRequestVersionId, out var detail))
            {
                if (_versions.TryGetValue(customizationRequestVersionId, out var version) &&
                    _productVersions.TryGetValue(version.ProductVersionId, out var productVersion))
                {
                    detail.Version = ToReadModel(version, productVersion);
                }

                return Task.FromResult<ProductionCustomizationVersionDetailReadModel?>(detail);
            }

            var queueItem = VersionQueueItems.FirstOrDefault(
                item => item.Version.CustomizationRequestVersionId == customizationRequestVersionId);
            if (queueItem is null)
            {
                return Task.FromResult<ProductionCustomizationVersionDetailReadModel?>(null);
            }

            return Task.FromResult<ProductionCustomizationVersionDetailReadModel?>(
                new ProductionCustomizationVersionDetailReadModel
                {
                    Version = queueItem.Version,
                    Request = queueItem.Request,
                    ProposalName = queueItem.ProposalName,
                    ProposalStatus = queueItem.ProposalStatus,
                    SourceProductVersion = queueItem.SourceProductVersion
                });
        }

        public Task<bool> TryMarkProductionReviewedAsync(
            ProductionVersionReviewUpdate update,
            CancellationToken cancellationToken = default)
        {
            if (!_versions.TryGetValue(update.CustomizationRequestVersionId, out var version))
            {
                return Task.FromResult(false);
            }

            if (version.Status != CustomizationVersionStatus.REVIEWING ||
                version.FeasibilityStatus != ProductionFeasibilityStatus.PENDING)
            {
                return Task.FromResult(false);
            }

            version.FeasibilityStatus = update.FeasibilityStatus;
            version.Status = update.VersionStatus;
            version.ProductionReviewedBy = update.ProductionReviewedBy;
            version.FeasibilityNote = update.FeasibilityNote;
            version.EstimatedProductionDays = update.EstimatedProductionDays;
            version.EstimatedAdditionalCost = update.EstimatedAdditionalCost;
            version.AdditionalCostReason = update.AdditionalCostReason;
            version.MaterialAvailable = update.MaterialAvailable;
            version.ProductionRiskNote = update.ProductionRiskNote;
            version.AlternativeMaterialNote = update.AlternativeMaterialNote;
            version.ProductionReviewedAt = update.ReviewedAt;
            version.UpdatedAt = update.ReviewedAt;
            return Task.FromResult(true);
        }

        public Task AddAsync(CustomizationRequestVersion entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _versions[entity.CustomizationRequestVersionId] = entity;
            return Task.CompletedTask;
        }

        public void Update(CustomizationRequestVersion entity)
        {
            _versions[entity.CustomizationRequestVersionId] = entity;
        }

        public IQueryable<CustomizationRequestVersion> Query()
            => _versions.Values.AsQueryable();
        public Task<CustomizationRequestVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => GetByIdForUpdateAsync(id, cancellationToken);
        public Task<IReadOnlyList<CustomizationRequestVersion>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomizationRequestVersion>>(_versions.Values.ToList());
        public Task AddRangeAsync(IEnumerable<CustomizationRequestVersion> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Remove(CustomizationRequestVersion entity) => _versions.Remove(entity.CustomizationRequestVersionId);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        private static CustomizationRequestVersionReadModel ToReadModel(
            CustomizationRequestVersion version,
            ProductVersion productVersion) => new()
        {
            CustomizationRequestVersionId = version.CustomizationRequestVersionId,
            CustomizationRequestId = version.CustomizationRequestId,
            ProductVersionId = version.ProductVersionId,
            VersionNo = version.VersionNo,
            CreatedByDesignerId = version.CreatedByDesignerId,
            VersionTitle = version.VersionTitle,
            DesignerNote = version.DesignerNote,
            Status = version.Status,
            ProductionReviewedBy = version.ProductionReviewedBy,
            FeasibilityStatus = version.FeasibilityStatus,
            FeasibilityNote = version.FeasibilityNote,
            EstimatedProductionDays = version.EstimatedProductionDays,
            EstimatedAdditionalCost = version.EstimatedAdditionalCost,
            AdditionalCostReason = version.AdditionalCostReason,
            MaterialAvailable = version.MaterialAvailable,
            ProductionRiskNote = version.ProductionRiskNote,
            AlternativeMaterialNote = version.AlternativeMaterialNote,
            SubmittedForReviewAt = version.SubmittedForReviewAt,
            ProductionReviewedAt = version.ProductionReviewedAt,
            ProductionRejectedAt = version.ProductionRejectedAt,
            AcceptedAt = version.AcceptedAt,
            WithdrawnAt = version.WithdrawnAt,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            ProductVersion = productVersion
        };
    }

    private sealed class FakeProposalRepository : IProposalRepository
    {
        private readonly ProposalProjectAccessReadModel? _project;

        public FakeProposalRepository(ProposalProjectAccessReadModel? project = null)
        {
            _project = project;
        }

        public Task<ProposalProjectAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_project?.ProjectId == projectId ? _project : null);

        public IQueryable<Proposal> Query() => Enumerable.Empty<Proposal>().AsQueryable();
        public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Proposal?>(null);
        public Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Proposal>>([]);
        public Task AddAsync(Proposal entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Proposal> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Proposal entity) { }
        public void Remove(Proposal entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<ProposalContextReadModel?> GetProposalContextAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalContextReadModel?>(null);
        public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProposalReadModel>> GetListAsync(ProposalListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalReadModel>>([]);
        public Task<int> CountListAsync(ProposalListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountScenesAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountScenesAsync(ProposalSceneListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task AddSceneAsync(ProposalScene scene, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(List<Guid> projectAreaIds, CancellationToken cancellationToken = default) => Task.FromResult<List<ProposalProjectAreaReadModel>>([]);
        public Task ReplaceSceneAreasAsync(Guid sceneId, List<Guid> projectAreaIds, DateTime now, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProposalDetailReadModel?> GetDetailAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalDetailReadModel?>(null);
        public Task<ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalDetailReadModel?>(null);
        public Task<IReadOnlyList<ProposalSceneReadModel>> GetScenesAsync(ProposalSceneListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalSceneReadModel>>([]);
        public Task<ProposalSceneDetailReadModel?> GetSceneDetailAsync(Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalSceneDetailReadModel?>(null);
        public Task<ProposalSceneContextReadModel?> GetSceneContextAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalSceneContextReadModel?>(null);
        public Task<ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalSceneContextReadModel?>(null);
        public Task<ProposalScene?> GetSceneEntityAsync(Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalScene?>(null);
        public Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalItem>>([]);
        public Task<IReadOnlyList<ProposalItemReadModel>> GetItemsAsync(ProposalItemListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalItemReadModel>>([]);
        public Task<int> CountItemsAsync(ProposalItemListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProposalItemDetailReadModel?> GetItemDetailAsync(Guid proposalItemId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalItemDetailReadModel?>(null);
        public Task<ProposalItem?> GetItemEntityAsync(Guid proposalItemId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalItem?>(null);
        public Task AddItemAsync(ProposalItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RemoveItem(ProposalItem item) { }
        public Task<Proposal?> GetProposalEntityAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult<Proposal?>(null);
        public Task RejectOtherActiveProposalsAsync(Guid projectId, Guid selectedProposalId, DateTime rejectedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasProposalWithActiveSceneAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasSelectedFinalProposalAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasActiveSceneAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> FileExistsAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ProjectAreaBelongsToProjectAsync(Guid projectAreaId, Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _role;
        private readonly Project? _project;

        public FakeProjectRepository(string? role, Project? project = null)
        {
            _role = role;
            _project = project;
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(_role);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_project?.ProjectId == id ? _project : null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
    }

    private sealed class FakeProductVersionRepository : IProductVersionRepository
    {
        private readonly List<ProductVersion> _versions;

        public FakeProductVersionRepository(IReadOnlyList<ProductVersion>? versions = null)
        {
            _versions = versions?.ToList() ?? [];
        }

        public int AddCallCount { get; private set; }
        public bool ProductExists { get; set; } = true;

        public Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.Any(version =>
                string.Equals(version.VersionCode, versionCode, StringComparison.Ordinal)));

        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(ProductExists);

        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(Guid productVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductVersionDetailReadModel?>(null);

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductVersionDetailReadModel>>([]);

        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountProjectSpecificByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.Count(version =>
                version.ProjectId == projectId &&
                version.VersionType == ProductVersionType.PROJECT_SPECIFIC));

        public IQueryable<ProductVersion> Query() => _versions.AsQueryable();
        public Task<ProductVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.FirstOrDefault(version => version.ProductVersionId == id));
        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductVersion>>(_versions);
        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _versions.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeCustomizationProjectFileRepository : IProjectFileRepository
    {
        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<FileMetadataReadModel?>(null);
        public IQueryable<StoredFile> Query() => Array.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new FileReferencePageReadModel { Items = [], Total = 0 });
        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default) => Task.FromResult<FileLinkReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(string referenceType, IReadOnlyList<Guid> referenceIds, bool customerVisibleOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);
        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(Guid productId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProductPreviewImageReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(Guid projectId, string query, int page, int limit, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(Guid projectId, string query, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> HasProjectFileWithTypesAsync(Guid projectId, IReadOnlyCollection<FileType> fileTypes, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? LastType { get; private set; }
        public IReadOnlyList<Guid> LastReceivers { get; private set; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            LastType = type;
            LastReceivers = receiverIds.ToList();
            return Task.CompletedTask;
        }
    }

    #endregion
}
