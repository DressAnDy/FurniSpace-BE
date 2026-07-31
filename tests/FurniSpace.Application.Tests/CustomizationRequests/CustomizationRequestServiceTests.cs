#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Constants.CustomizationRequests;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.CustomizationRequests;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class CustomizationRequestServiceTests
{
    [Fact]
    public async Task GetByProjectAsync_CustomerOwnerGetsRequests()
    {
        var ids = CreateIds();
        var repo = new FakeCustomizationRequestRepository
        {
            Items = [CreateRequest(ids)]
        };
        var service = CreateService(
            repo,
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
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetByProjectAsync(Guid.NewGuid(), Guid.Empty, new CustomizationRequestQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_AdminGetsRequests()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository { Items = [CreateRequest(ids)] },
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
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(DesignerRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.DesignerId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_UnassignedUserReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
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
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetByProjectAsync(Guid.NewGuid(), Guid.NewGuid(), new CustomizationRequestQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_ProductionFiltersInvisibleRequests()
    {
        var ids = CreateIds();
        var visible = CreateRequest(ids, status: CustomizationStatus.PRODUCTION_REVIEWING);
        var hidden = CreateRequest(ids, status: CustomizationStatus.SUBMITTED);
        var repo = new FakeCustomizationRequestRepository
        {
            Items = [visible, hidden],
            HasProductionVisibleRequest = true
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.ProductionId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(visible.CustomizationRequestId, result.Data.Items[0].CustomizationRequestId);
    }

    [Fact]
    public async Task GetProductionQueueAsync_ProductionUserGetsDefaultProductionReviewingQueue()
    {
        var ids = CreateIds();
        var queueItem = CreateQueueItem(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var repo = new FakeCustomizationRequestRepository { QueueItems = [queueItem] };
        var service = CreateService(repo, new FakeProposalRepository(), new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionQueueAsync(
            ids.ProductionId,
            new ProductionCustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationStatus.PRODUCTION_REVIEWING, result.Data.Items[0].Status);
        Assert.Equal("Cafe Project", result.Data.Items[0].Project.ProjectName);
        Assert.Equal("Cafe Proposal", result.Data.Items[0].Proposal.ProposalName);
        Assert.Equal("Dining Chair", result.Data.Items[0].SourceProductVersion.VersionName);
    }

    [Fact]
    public async Task GetProductionQueueAsync_ProductionUserCanFilterAcceptedRequests()
    {
        var ids = CreateIds();
        var repo = new FakeCustomizationRequestRepository
        {
            QueueItems =
            [
                CreateQueueItem(ids, CustomizationStatus.PRODUCTION_REVIEWING),
                CreateQueueItem(ids, CustomizationStatus.ACCEPTED)
            ]
        };
        var service = CreateService(repo, new FakeProposalRepository(), new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionQueueAsync(
            ids.ProductionId,
            new ProductionCustomizationRequestQueryDto { Status = "ACCEPTED" });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationStatus.ACCEPTED, result.Data.Items[0].Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_CustomerReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_SalesReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(SalesRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_DesignerReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_AdminWithAllStatusesReturnsAllItems()
    {
        var ids = CreateIds();
        var repo = new FakeCustomizationRequestRepository
        {
            QueueItems =
            [
                CreateQueueItem(ids, CustomizationStatus.PRODUCTION_REVIEWING),
                CreateQueueItem(ids, CustomizationStatus.SUBMITTED)
            ]
        };
        var service = CreateService(repo, new FakeProposalRepository(), new FakeProjectRepository(AdminRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto { Status = "ALL" });

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task GetProductionQueueAsync_ProductionInvalidStatusReturnsBadRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto { Status = "SUBMITTED" });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionQueueAsync(
            Guid.Empty,
            new ProductionCustomizationRequestQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_InvalidPaginationReturnsBadRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto { Page = 0 });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_AdminInvalidStatusReturnsBadRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto { Status = "NOT_A_STATUS" });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_AdminFiltersSpecificStatus()
    {
        var ids = CreateIds();
        var repo = new FakeCustomizationRequestRepository
        {
            QueueItems =
            [
                CreateQueueItem(ids, CustomizationStatus.PRODUCTION_REVIEWING),
                CreateQueueItem(ids, CustomizationStatus.ACCEPTED)
            ]
        };
        var service = CreateService(repo, new FakeProposalRepository(), new FakeProjectRepository(AdminRole));

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new ProductionCustomizationRequestQueryDto { Status = "ACCEPTED" });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CustomizationStatus.ACCEPTED, result.Data.Items[0].Status);
    }

    [Fact]
    public async Task GetDetailAsync_AuthorizedUserGetsSnapshot()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeCustomizationRequestRepository { Detail = detail },
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
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ProductionReviewerGetsRequest()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        detail.Status = CustomizationStatus.SUBMITTED;
        detail.ProductionReviewBy = ids.ProductionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository { Detail = detail },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.GetDetailAsync(detail.CustomizationRequestId, ids.ProductionId);

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_CustomerNotOwnerReturnsForbidden()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeCustomizationRequestRepository { Detail = detail },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.GetDetailAsync(detail.CustomizationRequestId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_IncludesApprovedProductVersion()
    {
        var ids = CreateIds();
        var approvedVersionId = Guid.NewGuid();
        var request = CreateRequest(ids, CustomizationStatus.ACCEPTED);
        request.ApprovedProductVersionId = approvedVersionId;
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = approvedVersionId,
            ProductId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Material = "Oak",
            Color = "Natural",
            EstimatedPrice = 1700000m,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository { Items = [request] },
            new FakeProposalRepository(project: CreateProject(ids)),
            new FakeProjectRepository(CustomerRole),
            productVersions: new FakeProductVersionRepository([approvedVersion]));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.CustomerId, new CustomizationRequestQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data!.Items[0].ApprovedProductVersion);
        Assert.Equal(approvedVersionId, result.Data.Items[0].ApprovedProductVersion!.ProductVersionId);
        Assert.Equal("PV-PRJ-000001-CUST-001", result.Data.Items[0].ApprovedProductVersion.VersionCode);
    }

    [Fact]
    public async Task GetDetailAsync_IncludesApprovedProductVersion()
    {
        var ids = CreateIds();
        var approvedVersionId = Guid.NewGuid();
        var detail = CreateDetail(ids, status: CustomizationStatus.ACCEPTED);
        detail.ApprovedProductVersionId = approvedVersionId;
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = approvedVersionId,
            ProductId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Material = "Oak",
            Color = "Natural",
            EstimatedPrice = 1700000m,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository { Detail = detail },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole),
            productVersions: new FakeProductVersionRepository([approvedVersion]));

        var result = await service.GetDetailAsync(detail.CustomizationRequestId, ids.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data!.ApprovedProductVersion);
        Assert.Equal(approvedVersionId, result.Data.ApprovedProductVersion!.ProductVersionId);
        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, result.Data.ApprovedProductVersion.VersionType);
    }

    [Fact]
    public async Task SubmitAsync_AdminCreatesRequestForProjectCustomer()
    {
        var ids = CreateIds();
        var adminId = Guid.NewGuid();
        var repo = new FakeCustomizationRequestRepository
        {
            SubmitContext = CreateSubmitContext(ids),
            DetailFactory = entity => CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, adminId, ValidSubmitRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(ids.CustomerId, repo.AddedRequest!.RequestedByCustomerId);
        Assert.Equal(CustomizationStatus.SUBMITTED, repo.AddedRequest.Status);
    }

    [Fact]
    public async Task SubmitAsync_CustomerOwnsPublishedProposal_CreatesSubmittedRequestAndNotifies()
    {
        var ids = CreateIds();
        var dispatcher = new FakeNotificationDispatcher();
        var repo = new FakeCustomizationRequestRepository
        {
            SubmitContext = CreateSubmitContext(ids),
            DetailFactory = entity => CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole),
            dispatcher);

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(CustomizationStatus.SUBMITTED, repo.AddedRequest!.Status);
        Assert.Equal(ids.CustomerId, repo.AddedRequest.RequestedByCustomerId);
        Assert.Equal(ids.ProjectId, repo.AddedRequest.ProjectId);
        Assert.Equal(1, repo.SaveChangesCallCount);
        Assert.Equal(NotificationType.CustomizationRequestSubmitted, dispatcher.LastType);
        Assert.Contains(ids.CustomerId, dispatcher.LastReceivers);
        Assert.Contains(ids.SalesId, dispatcher.LastReceivers);
        Assert.Contains(ids.DesignerId, dispatcher.LastReceivers);
    }

    [Fact]
    public async Task SubmitAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.Empty, ValidSubmitRequest());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task SubmitAsync_NonAuthorizedRoleReturnsForbidden()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.NewGuid(), ValidSubmitRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task SubmitAsync_AssignedDesignerCreatesRequestForProjectCustomer()
    {
        var ids = CreateIds();
        var repo = new FakeCustomizationRequestRepository
        {
            SubmitContext = CreateSubmitContext(ids),
            DetailFactory = entity => CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.DesignerId, ValidSubmitRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(ids.CustomerId, repo.AddedRequest!.RequestedByCustomerId);
        Assert.Equal(CustomizationStatus.SUBMITTED, repo.AddedRequest.Status);
    }

    [Fact]
    public async Task SubmitAsync_UnassignedDesignerReturnsDesignerNotAssignedError()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository { SubmitContext = CreateSubmitContext(ids) },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, Guid.NewGuid(), ValidSubmitRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.DesignerNotAssignedToProject, result.ErrorCode);
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
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ActiveCustomizationRequestAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_InvalidRequestReturnsInvalidCustomizationRequest()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.NewGuid(), new SubmitCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_ProposalItemMissingReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(Guid.NewGuid(), Guid.NewGuid(), ValidSubmitRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProposalItemNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_CustomerDoesNotOwnProjectReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository { SubmitContext = CreateSubmitContext(ids) },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, Guid.NewGuid(), ValidSubmitRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task SubmitAsync_ProposalAlreadySelectedReturnsBusinessError()
    {
        var ids = CreateIds();
        var context = CreateSubmitContext(ids);
        context.ProjectStatus = ProjectStatus.PROPOSAL_SELECTED;
        var service = CreateService(
            new FakeCustomizationRequestRepository { SubmitContext = context },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProposalAlreadySelected, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_SelectedProposalStatusReturnsBusinessError()
    {
        var ids = CreateIds();
        var context = CreateSubmitContext(ids);
        context.ProposalStatus = ProposalStatus.SELECTED;
        var service = CreateService(
            new FakeCustomizationRequestRepository { SubmitContext = context },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProposalAlreadySelected, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_DraftProposalReturnsInvalidCustomizationRequest()
    {
        var ids = CreateIds();
        var context = CreateSubmitContext(ids);
        context.ProposalStatus = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeCustomizationRequestRepository { SubmitContext = context },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_QuotationExistsReturnsBusinessError()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                SubmitContext = CreateSubmitContext(ids),
                HasQuotation = true
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.SubmitAsync(ids.ProposalItemId, ids.CustomerId, ValidSubmitRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.QuotationAlreadyCreated, result.ErrorCode);
    }

    [Fact]
    public async Task DesignerReviewAsync_AssignedDesignerMovesSubmittedToDesignReviewing()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var dispatcher = new FakeNotificationDispatcher();
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId)
        };
        var productionReceiver = Guid.NewGuid();
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, [productionReceiver]),
            dispatcher);

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible with dark oak." });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, entity.Status);
        Assert.Equal(ids.DesignerId, entity.DesignerId);
        Assert.Equal("Possible with dark oak.", entity.DesignerSpecNote);
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal(1, repo.SaveChangesCallCount);
        Assert.Null(dispatcher.LastType);
    }

    [Fact]
    public async Task DesignerReviewAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.DesignerReviewAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible." });

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task DesignerReviewAsync_DesignReviewingWithoutProductVersionReturnsBadRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible." });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationProductVersionRequired, result.ErrorCode);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, entity.Status);
    }

    [Fact]
    public async Task DesignerReviewAsync_DesignReviewingWithProductVersionMovesToProduction()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var approvedVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ApprovedProductVersionId = approvedVersionId;
        entity.ProductVersionId = originalVersionId;
        var productVersions = new FakeProductVersionRepository(
        [
            new ProductVersion
            {
                ProductVersionId = originalVersionId,
                ProductId = productId,
                VersionCode = "PV-STD-001",
                VersionName = "Standard",
                Status = ProductStatus.ACTIVE
            },
            new ProductVersion
            {
                ProductVersionId = approvedVersionId,
                ProductId = productId,
                ProjectId = ids.ProjectId,
                VersionCode = "PV-PRJ-000001-CUST-001",
                VersionName = "Custom",
                VersionType = ProductVersionType.PROJECT_SPECIFIC,
                Status = ProductStatus.ACTIVE
            }
        ]);
        var dispatcher = new FakeNotificationDispatcher();
        var productionReceiver = Guid.NewGuid();
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole, [productionReceiver]),
            dispatcher,
            productVersions: productVersions);

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Ready for production." });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.PRODUCTION_REVIEWING, entity.Status);
        Assert.Equal(NotificationType.CustomizationDesignerReviewed, dispatcher.LastType);
        Assert.Contains(productionReceiver, dispatcher.LastReceivers);
    }

    [Fact]
    public async Task DesignerReviewAsync_BlankNoteReturnsInvalidCustomizationRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = " " });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task DesignerReviewAsync_UnassignedDesignerReturnsForbidden()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible." });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task DesignerReviewAsync_AcceptedRequestReturnsInvalidTransition()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.ACCEPTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.ACCEPTED)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.DesignerReviewAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible." });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationTransition, result.ErrorCode);
    }

    [Fact]
    public async Task DesignerReviewAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.DesignerReviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible." });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_FeasibleRequestMovesToCustomerApproval()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.ProductionId,
            FeasibleProductionReview());

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL, entity.Status);
        Assert.Equal(ids.ProductionId, entity.ProductionReviewBy);
        Assert.Equal(5, entity.EstimatedProductionDays);
        Assert.Equal(1500000, entity.EstimatedAdditionalCost);
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal(1, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task ProductionReviewAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            Guid.NewGuid(),
            Guid.Empty,
            FeasibleProductionReview());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task ProductionReviewAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FeasibleProductionReview());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_AdminCanReview()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            FeasibleProductionReview());

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task ProductionReviewAsync_CostWithoutReasonReturnsAdditionalCostReasonRequired()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var request = FeasibleProductionReview();
        request.AdditionalCostReason = null;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(entity.CustomizationRequestId, ids.ProductionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.AdditionalCostReasonRequired, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_NotFeasibleMovesToNotFeasible()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.ProductionId,
            new ProductionReviewCustomizationRequestDto
            {
                Result = "NOT_FEASIBLE",
                MaterialAvailable = false,
                FeasibilityNote = "Material unavailable."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.NOT_FEASIBLE, entity.Status);
    }

    [Fact]
    public async Task ProductionReviewAsync_InvalidResultReturnsInvalidTransition()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.ProductionId,
            new ProductionReviewCustomizationRequestDto { Result = "UNKNOWN" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_FeasibleMissingCostReturnsCustomizationCostRequired()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var request = FeasibleProductionReview();
        request.EstimatedAdditionalCost = null;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(entity.CustomizationRequestId, ids.ProductionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationCostRequired, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_NotFeasibleWithMaterialAvailableReturnsMaterialNotAvailable()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.ProductionId,
            new ProductionReviewCustomizationRequestDto
            {
                Result = "NOT_FEASIBLE",
                MaterialAvailable = true
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.MaterialNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_WrongRoleReturnsForbidden()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            FeasibleProductionReview());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task ProductionReviewAsync_InvalidStatusReturnsInvalidTransition()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(
            entity.CustomizationRequestId,
            ids.ProductionId,
            FeasibleProductionReview());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReviewAsync_FeasibleWithoutMaterialReturnsMaterialNotAvailable()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var request = FeasibleProductionReview();
        request.MaterialAvailable = false;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(ProductionRole));

        var result = await service.ProductionReviewAsync(entity.CustomizationRequestId, ids.ProductionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.MaterialNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_AcceptMarksRequestAcceptedWithoutProposalItemChanges()
    {
        var ids = CreateIds();
        var approvedVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        entity.EstimatedAdditionalCost = 250000m;
        entity.ApprovedProductVersionId = approvedVersionId;
        entity.ProductVersionId = ids.ProductVersionId;
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = approvedVersionId,
            ProductId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            Material = "Walnut",
            Color = "Dark",
            Width = 65m,
            EstimatedPrice = 1250000m,
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Status = ProductStatus.ACTIVE
        };
        var productVersions = new FakeProductVersionRepository([approvedVersion]);
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(
                ids,
                entity.CustomizationRequestId,
                CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions);

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.ACCEPTED, entity.Status);
        Assert.Equal(approvedVersionId, entity.ApprovedProductVersionId);
        Assert.Equal(0, productVersions.AddCallCount);
        Assert.NotNull(entity.CustomerAcceptedAt);
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal(1, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_AssignedDesignerCreatesSuccessfully()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        entity.EstimatedAdditionalCost = 250000m;
        entity.RequestedMaterial = "Dark oak";
        entity.RequestedColor = "Brown";
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = originalVersionId,
            ProductId = productId,
            VersionCode = "PV-STD-001",
            VersionName = "Standard Chair",
            Material = "Oak",
            Color = "Natural",
            Width = 50m,
            Height = 80m,
            Depth = 45m,
            DimensionUnit = "cm",
            EstimatedPrice = 1000000m,
            Status = ProductStatus.ACTIVE
        };
        var productVersions = new FakeProductVersionRepository([sourceVersion]);
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions,
            unitOfWork: TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                repo.SaveChangesAsync,
                _ => Task.CompletedTask,
                _ => Task.CompletedTask));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Cafe Chair - Custom Walnut 65cm",
                Material = "Walnut wood",
                Color = "Dark brown",
                Width = 65m,
                EstimatedPrice = 3200000m,
                DimensionUnit = "cm"
            });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Cafe Chair - Custom Walnut 65cm", result.Data.ProductVersion.VersionName);
        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, result.Data.ProductVersion.VersionType);
        Assert.True(result.Data.ProductVersion.IsProjectSpecific);
        Assert.False(result.Data.ProductVersion.IsPublic);
        Assert.Equal(productId, result.Data.ProductVersion.ProductId);
        Assert.Equal(ids.ProjectId, result.Data.ProductVersion.ProjectId);
        Assert.Equal(result.Data.ProductVersion.ProductVersionId, entity.ApprovedProductVersionId);
        Assert.Equal(originalVersionId, entity.ProductVersionId);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, entity.Status);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, result.Data.CustomizationStatus);
        Assert.Equal(1, productVersions.AddCallCount);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_AdminCreatesSuccessfully()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = originalVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-STD-001",
            VersionName = "Standard Chair",
            EstimatedPrice = 1000000m,
            Status = ProductStatus.ACTIVE
        };
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository([sourceVersion]),
            unitOfWork: TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                repo.SaveChangesAsync,
                _ => Task.CompletedTask,
                _ => Task.CompletedTask));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new CreateCustomizationProductVersionRequestDto
            {
                DimensionUnit = "cm",
                VersionName = "Admin Custom Chair"
            });

        Assert.Equal(201, result.Status);
        Assert.NotNull(entity.ApprovedProductVersionId);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_UnassignedDesignerReturnsProjectAccessDenied()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new CreateCustomizationProductVersionRequestDto { DimensionUnit = "cm",
                VersionName = "Custom" });

        Assert.Equal(403, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProjectAccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_NotInDesignReviewReturnsConflict()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { DimensionUnit = "cm",
                VersionName = "Custom" });

        Assert.Equal(409, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationNotInDesignReview, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_MissingSourceProductVersionReturnsNotFound()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = Guid.NewGuid();
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { DimensionUnit = "cm",
                VersionName = "Custom" });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.SourceProductVersionNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_DuplicateVersionCodeReturnsConflict()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var existingCode = "CHAIR-CUSTOM-001";
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = originalVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-STD-001",
            VersionName = "Standard",
            Status = ProductStatus.ACTIVE
        };
        var existingVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = existingCode,
            VersionName = "Existing"
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository([sourceVersion, existingVersion]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                DimensionUnit = "cm",
                VersionName = "Custom",
                VersionCode = existingCode
            });

        Assert.Equal(409, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.VersionCodeAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_InvalidDimensionsReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                DimensionUnit = "cm",
                VersionName = "Custom",
                Width = 0m
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidProductDimensions, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_InvalidDimensionUnitReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "inch"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidDimensionUnit, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_LinksModelAndPreviewFiles()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var modelFileId = Guid.NewGuid();
        var previewFileId1 = Guid.NewGuid();
        var previewFileId2 = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var projectFiles = new FakeCustomizationProjectFileRepository();
        projectFiles.Files[modelFileId] = new FileMetadataReadModel
        {
            FileId = modelFileId,
            OriginalFileName = "chair.glb",
            FileType = FileType.MODEL_3D,
            Status = FileStatus.ACTIVE,
            Visibility = FileVisibility.CUSTOMER_VISIBLE
        };
        projectFiles.Files[previewFileId1] = new FileMetadataReadModel
        {
            FileId = previewFileId1,
            OriginalFileName = "preview-1.png",
            FileType = FileType.PRODUCT_PREVIEW,
            Status = FileStatus.ACTIVE
        };
        projectFiles.Files[previewFileId2] = new FileMetadataReadModel
        {
            FileId = previewFileId2,
            OriginalFileName = "preview-2.png",
            FileType = FileType.PRODUCT_PREVIEW,
            Status = FileStatus.ACTIVE
        };
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]),
            unitOfWork: TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                repo.SaveChangesAsync,
                _ => Task.CompletedTask,
                _ => Task.CompletedTask),
            projectFiles: projectFiles);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                DimensionUnit = "cm",
                VersionName = "Custom with files",
                ModelFileId = modelFileId,
                PreviewFileIds = [previewFileId1, previewFileId2]
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(3, projectFiles.AddedLinks.Count);
        Assert.Contains(projectFiles.AddedLinks, link =>
            link.FileType == FileType.MODEL_3D &&
            link.ReferenceType == "PRODUCT_VERSION");
        Assert.Equal(0, projectFiles.AddedLinks.Single(link => link.FileId == previewFileId1).DisplayOrder);
        Assert.True(projectFiles.AddedLinks.Single(link => link.FileId == previewFileId1).IsPrimary);
        Assert.Equal(1, projectFiles.AddedLinks.Single(link => link.FileId == previewFileId2).DisplayOrder);
        Assert.False(projectFiles.AddedLinks.Single(link => link.FileId == previewFileId2).IsPrimary);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_InvalidModelFileTypeReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var modelFileId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var projectFiles = new FakeCustomizationProjectFileRepository();
        projectFiles.Files[modelFileId] = new FileMetadataReadModel
        {
            FileId = modelFileId,
            OriginalFileName = "photo.png",
            FileType = FileType.PRODUCT_PREVIEW,
            Status = FileStatus.ACTIVE
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]),
            projectFiles: projectFiles);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                DimensionUnit = "cm",
                VersionName = "Custom",
                ModelFileId = modelFileId
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidModelFileType, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_RetryReturnsExistingProductVersion()
    {
        var ids = CreateIds();
        var approvedVersionId = Guid.NewGuid();
        var originalVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ApprovedProductVersionId = approvedVersionId;
        entity.ProductVersionId = originalVersionId;
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = originalVersionId,
            ProductId = productId,
            VersionCode = "PV-STD-001",
            VersionName = "Standard",
            Status = ProductStatus.ACTIVE
        };
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = approvedVersionId,
            ProductId = productId,
            ProjectId = ids.ProjectId,
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Existing Custom",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            IsProjectSpecific = true,
            IsPublic = false,
            Status = ProductStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        var productVersions = new FakeProductVersionRepository([sourceVersion, approvedVersion]);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { DimensionUnit = "cm",
                VersionName = "Ignored" });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(approvedVersionId, result.Data.ProductVersion.ProductVersionId);
        Assert.Equal(0, productVersions.AddCallCount);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_LinkConflictReturnsConflict()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ApprovedProductVersionId = Guid.NewGuid();
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { DimensionUnit = "cm",
                VersionName = "Custom" });

        Assert.Equal(409, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationProductVersionLinkConflict, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.CreateCustomizationProductVersionAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new CreateCustomizationProductVersionRequestDto { VersionName = "Custom", DimensionUnit = "cm" });

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(CreateIds())));

        var result = await service.CreateCustomizationProductVersionAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CreateCustomizationProductVersionRequestDto { VersionName = "Custom", DimensionUnit = "cm" });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_EmptySourceProductVersionIdReturnsBadRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = Guid.Empty;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { VersionName = "Custom", DimensionUnit = "cm" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.SourceProductVersionRequired, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_MissingSourceProductReturnsNotFound()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var productVersions = new FakeProductVersionRepository(
        [
            new ProductVersion
            {
                ProductVersionId = originalVersionId,
                ProductId = Guid.NewGuid(),
                VersionCode = "PV-STD-001",
                VersionName = "Standard",
                Status = ProductStatus.ACTIVE
            }
        ])
        {
            ProductExists = false
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: productVersions);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { VersionName = "Custom", DimensionUnit = "cm" });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.SourceProductNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_MissingProjectReturnsNotFound()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto { VersionName = "Custom", DimensionUnit = "cm" });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_TooManyPreviewFilesReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var previewFileIds = Enumerable.Range(0, CustomizationRequestServiceConstants.MaxProductVersionPreviewFileCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                PreviewFileIds = previewFileIds
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_DuplicatePreviewFileIdsReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var duplicatePreviewId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                PreviewFileIds = [duplicatePreviewId, duplicatePreviewId]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_PreviewFileNotFoundReturnsNotFound()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                PreviewFileIds = [Guid.NewGuid()]
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.PreviewFileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_PreviewFileNotActiveReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var previewFileId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var projectFiles = new FakeCustomizationProjectFileRepository();
        projectFiles.Files[previewFileId] = new FileMetadataReadModel
        {
            FileId = previewFileId,
            OriginalFileName = "preview.png",
            FileType = FileType.PRODUCT_PREVIEW,
            Status = FileStatus.ARCHIVED
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]),
            projectFiles: projectFiles);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                PreviewFileIds = [previewFileId]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.PreviewFileNotActive, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_InvalidPreviewFileTypeReturnsBadRequest()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var previewFileId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var projectFiles = new FakeCustomizationProjectFileRepository();
        projectFiles.Files[previewFileId] = new FileMetadataReadModel
        {
            FileId = previewFileId,
            OriginalFileName = "model.glb",
            FileType = FileType.MODEL_3D,
            Status = FileStatus.ACTIVE
        };
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]),
            projectFiles: projectFiles);

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                PreviewFileIds = [previewFileId]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationRequest, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_ModelFileNotFoundReturnsNotFound()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm",
                ModelFileId = Guid.NewGuid()
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.ModelFileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCustomizationProductVersionAsync_SaveFailureReturnsInternalServerError()
    {
        var ids = CreateIds();
        var originalVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.DESIGN_REVIEWING);
        entity.ProductVersionId = originalVersionId;
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.DESIGN_REVIEWING)
        };
        var rollbackCalled = false;
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole, project: CreateProjectEntity(ids)),
            productVersions: new FakeProductVersionRepository(
            [
                new ProductVersion
                {
                    ProductVersionId = originalVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionCode = "PV-STD-001",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE
                }
            ]),
            unitOfWork: TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => throw new InvalidOperationException("Save failed."),
                _ => Task.CompletedTask,
                _ =>
                {
                    rollbackCalled = true;
                    return Task.CompletedTask;
                }));

        var result = await service.CreateCustomizationProductVersionAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Custom",
                DimensionUnit = "cm"
            });

        Assert.Equal(500, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomProductVersionCreationFailed, result.ErrorCode);
        Assert.True(rollbackCalled);
    }

    [Fact]
    public async Task CustomerDecisionAsync_RejectMarksRequestRejected()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        entity.EstimatedAdditionalCost = 250000m;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto
            {
                Decision = "REJECT",
                RejectReason = "Cost is too high."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.REJECTED_BY_CUSTOMER, entity.Status);
        Assert.NotNull(entity.CustomerRejectedAt);
    }

    [Fact]
    public async Task CustomerDecisionAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CustomerDecisionAsync_NotOwnerReturnsForbidden()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CustomerDecisionAsync_NotReadyReturnsCustomizationNotReady()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationNotReadyForFinalApproval, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_AcceptNotFeasibleReturnsCustomizationNotFeasible()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.NOT_FEASIBLE);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.NOT_FEASIBLE)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationNotFeasible, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_AcceptWithoutCostReturnsCustomizationCostNotApproved()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationCostNotApproved, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_InvalidDecisionReturnsInvalidCustomizationDecision()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "MAYBE" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationDecision, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_RejectWithoutReasonReturnsInvalidCustomizationDecision()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "REJECT" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationDecision, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_AcceptWithoutLinkedProductVersionReturnsBadRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL);
        entity.EstimatedAdditionalCost = 250000m;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(
                    ids,
                    entity.CustomizationRequestId,
                    CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationProductVersionRequired, result.ErrorCode);
    }

    [Fact]
    public async Task CustomerDecisionAsync_AlreadyAcceptedIsIdempotent()
    {
        var ids = CreateIds();
        var approvedVersionId = Guid.NewGuid();
        var entity = CreateEntity(ids, CustomizationStatus.ACCEPTED);
        entity.ApprovedProductVersionId = approvedVersionId;
        entity.EstimatedAdditionalCost = 250000m;
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = approvedVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Status = ProductStatus.ACTIVE
        };
        var productVersions = new FakeProductVersionRepository([approvedVersion]);
        var detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.ACCEPTED);
        detail.ApprovedProductVersionId = approvedVersionId;
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = detail
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole),
            productVersions: productVersions);

        var result = await service.CustomerDecisionAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" });

        Assert.Equal(200, result.Status);
        Assert.Equal(0, productVersions.AddCallCount);
        Assert.NotNull(result.Data!.ApprovedProductVersion);
        Assert.Equal(approvedVersionId, result.Data.ApprovedProductVersion!.ProductVersionId);
    }

    [Fact]
    public async Task CancelAsync_CustomerOwnerCancelsSubmittedRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var repo = new FakeCustomizationRequestRepository
        {
            ExistingEntity = entity,
            Detail = CreateDetail(ids, entity.CustomizationRequestId)
        };
        var service = CreateService(
            repo,
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto { CancelReason = "No longer needed." });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.CANCELLED, entity.Status);
        Assert.Equal("No longer needed.", entity.ProductionRiskNote);
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal(1, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_AssignedDesignerCancelsRequest()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.PRODUCTION_REVIEWING);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId, CustomizationStatus.PRODUCTION_REVIEWING)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(DesignerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.DesignerId,
            new CancelCustomizationRequestDto { CancelReason = "Invalid request." });

        Assert.Equal(200, result.Status);
        Assert.Equal(CustomizationStatus.CANCELLED, entity.Status);
    }

    [Fact]
    public async Task CancelAsync_EmptyUserReturnsUnauthorized()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new CancelCustomizationRequestDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CancelAsync_MissingRequestReturnsNotFound()
    {
        var service = CreateService(
            new FakeCustomizationRequestRepository(),
            new FakeProposalRepository(),
            new FakeProjectRepository(AdminRole));

        var result = await service.CancelAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CancelCustomizationRequestDto());

        Assert.Equal(404, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_UnassignedStaffReturnsForbidden()
    {
        var ids = CreateIds();
        var entity = CreateEntity(ids, CustomizationStatus.SUBMITTED);
        var service = CreateService(
            new FakeCustomizationRequestRepository
            {
                ExistingEntity = entity,
                Detail = CreateDetail(ids, entity.CustomizationRequestId)
            },
            new FakeProposalRepository(),
            new FakeProjectRepository(SalesRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            Guid.NewGuid(),
            new CancelCustomizationRequestDto());

        Assert.Equal(403, result.Status);
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
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationAlreadyAccepted, result.ErrorCode);
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
            new FakeProposalRepository(),
            new FakeProjectRepository(CustomerRole));

        var result = await service.CancelAsync(
            entity.CustomizationRequestId,
            ids.CustomerId,
            new CancelCustomizationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.InvalidCustomizationTransition, result.ErrorCode);
    }

    private static Project CreateProjectEntity(TestIds ids) => new()
    {
        ProjectId = ids.ProjectId,
        ProjectCode = "PRJ-000001",
        CustomerId = ids.CustomerId
    };

    private static CustomizationRequestService CreateService(
        FakeCustomizationRequestRepository customizationRequests,
        FakeProposalRepository proposals,
        FakeProjectRepository projects,
        FakeNotificationDispatcher? dispatcher = null,
        FakeProductVersionRepository? productVersions = null,
        IUnitOfWork? unitOfWork = null,
        FakeCustomizationProjectFileRepository? projectFiles = null)
    {
        return new CustomizationRequestService(
            customizationRequests,
            proposals,
            projects,
            productVersions ?? new FakeProductVersionRepository(),
            projectFiles ?? new FakeCustomizationProjectFileRepository(),
            dispatcher ?? new FakeNotificationDispatcher(),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(customizationRequests.SaveChangesAsync));
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

    private static CustomizationRequestReadModel CreateRequest(
        TestIds ids,
        CustomizationStatus status = CustomizationStatus.SUBMITTED) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = ids.ProjectId,
        ProposalId = ids.ProposalId,
        ProductVersionId = ids.ProductVersionId,
        CustomerId = ids.CustomerId,
        AssignedSalesId = ids.SalesId,
        AssignedDesignerId = ids.DesignerId,
        ProjectName = "Cafe",
        RequestTitle = "Change chair material",
        Status = status
    };

    private static ProductionCustomizationRequestQueueReadModel CreateQueueItem(
        TestIds ids,
        CustomizationStatus status) => new()
    {
        Request = new CustomizationRequestReadModel
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            ProposalId = ids.ProposalId,
            ProductVersionId = ids.ProductVersionId,
            RequestTitle = "Change chair material",
            Status = status,
            ProjectName = "Cafe Project",
            CustomerId = ids.CustomerId,
            AssignedSalesId = ids.SalesId,
            AssignedDesignerId = ids.DesignerId,
            UpdatedAt = DateTime.UtcNow
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
            ProductVersionId = request.ProductVersionId,
            CustomerId = request.CustomerId,
            AssignedSalesId = request.AssignedSalesId,
            AssignedDesignerId = request.AssignedDesignerId,
            RequestTitle = request.RequestTitle,
            Status = status,
            SourceProductVersion = new ProductVersion
            {
                ProductVersionId = request.ProductVersionId,
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
        ProductVersionId = ids.ProductVersionId,
        RequestedByCustomerId = ids.CustomerId,
        RequestTitle = "Change chair material",
        RequestedMaterial = "Dark oak wood",
        Status = status
    };

    private static ProposalItem CreateProposalItem(TestIds ids) => new()
    {
        ProposalItemId = ids.ProposalItemId,
        ProposalId = ids.ProposalId,
        ItemName = "Chair",
        Quantity = 2,
        UnitPriceSnapshot = 1000000m,
        TotalPriceSnapshot = 2000000m,
        IsCustomized = false
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

    private static ProductionReviewCustomizationRequestDto FeasibleProductionReview() => new()
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

    private sealed class FakeCustomizationRequestRepository : ICustomizationRequestRepository
    {
        public IReadOnlyList<CustomizationRequestReadModel> Items { get; init; } = [];
        public IReadOnlyList<ProductionCustomizationRequestQueueReadModel> QueueItems { get; init; } = [];
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
                .Where(item => !query.ProductVersionId.HasValue || item.ProductVersionId == query.ProductVersionId)
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
            if (detail is not null && ExistingEntity?.CustomizationRequestId == customizationRequestId)
            {
                CopyEntityState(ExistingEntity, detail);
            }

            return Task.FromResult(detail?.CustomizationRequestId == customizationRequestId ? detail : null);
        }

        public Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(
            Guid proposalItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SubmitContext?.ProposalItemId == proposalItemId ? SubmitContext : null);
        }

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

        public Task<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>> GetProductionQueueAsync(
            ProductionCustomizationRequestQueueQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var result = QueueItems.AsEnumerable();
            if (query.Statuses is { Count: > 0 })
            {
                result = result.Where(
                    item =>
                        item.Request.Status.HasValue &&
                        query.Statuses.Contains(item.Request.Status.Value));
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
                result = result.Where(item => item.Request.MaterialAvailable == query.MaterialAvailable.Value);
            }

            var items = result
                .OrderByDescending(item => item.Request.UpdatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>>(items);
        }

        public Task<int> CountProductionQueueAsync(
            ProductionCustomizationRequestQueueQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var result = QueueItems.AsEnumerable();
            if (query.Statuses is { Count: > 0 })
            {
                result = result.Where(
                    item =>
                        item.Request.Status.HasValue &&
                        query.Statuses.Contains(item.Request.Status.Value));
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
                result = result.Where(item => item.Request.MaterialAvailable == query.MaterialAvailable.Value);
            }

            return Task.FromResult(result.Count());
        }

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
        public Task<IReadOnlyList<CustomizationRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomizationRequest>>([]);
        public Task AddRangeAsync(IEnumerable<CustomizationRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(CustomizationRequest entity)
        {
            UpdateCallCount++;
        }
        public void Remove(CustomizationRequest entity) { }

        private static void CopyEntityState(
            CustomizationRequest entity,
            CustomizationRequestDetailReadModel detail)
        {
            detail.DesignerId = entity.DesignerId;
            detail.DesignerSpecNote = entity.DesignerSpecNote;
            detail.ProductionReviewBy = entity.ProductionReviewBy;
            detail.FeasibilityNote = entity.FeasibilityNote;
            detail.EstimatedProductionDays = entity.EstimatedProductionDays;
            detail.EstimatedAdditionalCost = entity.EstimatedAdditionalCost;
            detail.AdditionalCostReason = entity.AdditionalCostReason;
            detail.MaterialAvailable = entity.MaterialAvailable;
            detail.ProductionRiskNote = entity.ProductionRiskNote;
            detail.Status = entity.Status;
            detail.CustomerAcceptedAt = entity.CustomerAcceptedAt;
            detail.CustomerRejectedAt = entity.CustomerRejectedAt;
            detail.UpdatedAt = entity.UpdatedAt;
        }
    }

    private sealed class FakeProposalRepository : IProposalRepository
    {
        private readonly ProposalProjectAccessReadModel? _project;
        private readonly ProposalItem? _proposalItem;

        public FakeProposalRepository(
            ProposalProjectAccessReadModel? project = null,
            ProposalItem? proposalItem = null)
        {
            _project = project;
            _proposalItem = proposalItem;
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
        public Task<List<ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(
            List<Guid> projectAreaIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<List<ProposalProjectAreaReadModel>>([]);
        public Task ReplaceSceneAreasAsync(
            Guid sceneId,
            List<Guid> projectAreaIds,
            DateTime now,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
        public Task<ProposalItem?> GetItemEntityAsync(Guid proposalItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_proposalItem?.ProposalItemId == proposalItemId ? _proposalItem : null);
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
        private readonly IReadOnlyList<Guid> _activeAccountIds;
        private readonly Project? _project;

        public FakeProjectRepository(
            string? role,
            IReadOnlyList<Guid>? activeAccountIds = null,
            Project? project = null)
        {
            _role = role;
            _activeAccountIds = activeAccountIds ?? [];
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
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult(_activeAccountIds);
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

        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductVersionDetailReadModel?>(null);

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductVersionDetailReadModel>>(
                _versions
                    .Where(version => productVersionIds.Contains(version.ProductVersionId))
                    .Select(version => new ProductVersionDetailReadModel
                    {
                        ProductVersionId = version.ProductVersionId,
                        ProductId = version.ProductId,
                        VersionCode = version.VersionCode,
                        VersionName = version.VersionName,
                        VersionType = version.VersionType,
                        Material = version.Material,
                        Color = version.Color,
                        Width = version.Width,
                        Height = version.Height,
                        Depth = version.Depth,
                        EstimatedPrice = version.EstimatedPrice,
                        IsDefault = version.IsDefault,
                        IsPublic = version.IsPublic,
                        IsProjectSpecific = version.IsProjectSpecific,
                        Status = version.Status
                    })
                    .ToList());
        }

        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountProjectSpecificByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
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
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeCustomizationProjectFileRepository : IProjectFileRepository
    {
        public Dictionary<Guid, FileMetadataReadModel> Files { get; } = [];
        public List<FileLink> AddedLinks { get; } = [];

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        {
            AddedLinks.Add(fileLink);
            return Task.CompletedTask;
        }

        public Task<FileMetadataReadModel?> GetFileMetadataAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            Files.TryGetValue(fileId, out var metadata);
            return Task.FromResult(metadata);
        }

        public IQueryable<StoredFile> Query() => Array.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
            string referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
            FileReferenceQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new FileReferencePageReadModel { Items = [], Total = 0 });
        public Task<FileLinkReadModel?> GetFileLinkAsync(
            Guid fileLinkId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<FileLinkReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
        public Task<int> CountProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);
        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
            Guid productId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductPreviewImageReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<int> CountProductVersionPreviewFilesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
            Guid projectId,
            string query,
            int page,
            int limit,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(
            Guid projectId,
            string query,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<bool> HasProjectFileWithTypesAsync(
            Guid projectId,
            IReadOnlyCollection<FileType> fileTypes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
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
}
