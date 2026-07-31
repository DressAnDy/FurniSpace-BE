using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.Proposals.ProposalServiceConstants;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;
using RoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;

namespace FurniSpace.Application.Services.Proposals;

public sealed class ProposalService : IProposalService
{
    private readonly IProposalRepository _proposals;
    private readonly ICustomizationRequestRepository? _customizationRequests;
    private readonly IProjectRepository _projects;
    private readonly IProductVersionRepository _productVersions;
    private readonly RoomPlannerSceneRepository? _roomPlannerScenes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<ProposalService>? _logger;

    public ProposalService(
        IProposalRepository proposals,
        IProjectRepository projects,
        IProductVersionRepository productVersions,
        IUnitOfWork unitOfWork,
        ProposalServiceDependencies? dependencies = null)
    {
        _proposals = proposals;
        _customizationRequests = dependencies?.CustomizationRequests;
        _projects = projects;
        _productVersions = productVersions;
        _roomPlannerScenes = dependencies?.RoomPlannerScenes;
        _unitOfWork = unitOfWork;
        _notifications = dependencies?.Notifications;
        _logger = dependencies?.Logger;
    }

    public async Task<ServiceResult<ProposalDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProposalDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProposalDto>.BadRequest(validationErrors);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsProposalStaff(roleName))
        {
            return ServiceResult<ProposalDto>.Forbidden("You do not have access to create proposals for this project.");
        }

        var project = await _proposals.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProposalDto>.NotFound("Project not found.");
        }

        if (!project.AssignedDesignerId.HasValue)
        {
            return ServiceResult<ProposalDto>.Failure(Error.BadRequest(
                "DESIGNER_NOT_ASSIGNED",
                "Project must have an assigned designer before creating a proposal."));
        }

        if (project.ProjectStatus != ProjectStatus.PROPOSAL_CONSULTING)
        {
            return ServiceResult<ProposalDto>.Failure(Error.BadRequest(
                "INVALID_PROJECT_STATUS",
                "Proposal can only be created when project status is PROPOSAL_CONSULTING."));
        }

        if (!CanStaffAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProposalDto>.Forbidden("You do not have access to create proposals for this project.");
        }

        var now = DateTime.UtcNow;
        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalName = request.ProposalName.Trim(),
            Description = NormalizeOptional(request.Description),
            VersionNo = await _proposals.CountByProjectAsync(projectId, cancellationToken) + 1,
            Status = ProposalStatus.DRAFT,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _proposals.AddAsync(proposal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProposalDto>.Created(
            proposal.Adapt<ProposalDto>(),
            "Proposal created successfully.");
    }

    public async Task<ServiceResult<ProposalListResponseDto>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        ProposalListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProposalListResponseDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalListResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var paginationError = ValidatePagination(query.Page, query.Limit);
        if (paginationError is not null)
        {
            return ServiceResult<ProposalListResponseDto>.BadRequest(paginationError);
        }

        var project = await _proposals.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProposalListResponseDto>.NotFound("Project not found.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProjectProposals(project, currentUserId, roleName))
        {
            return ServiceResult<ProposalListResponseDto>.Forbidden("You do not have access to view project proposals.");
        }

        var repositoryQuery = new ProposalListQueryReadModel
        {
            ProjectId = projectId,
            Status = query.Status,
            Page = query.Page,
            Limit = query.Limit,
            CustomerVisibleOnly = IsCustomer(roleName)
        };
        var proposals = await _proposals.GetListAsync(repositoryQuery, cancellationToken);
        var total = await _proposals.CountListAsync(repositoryQuery, cancellationToken);

        return ServiceResult<ProposalListResponseDto>.Success(
            new ProposalListResponseDto
            {
                Items = proposals.Adapt<List<ProposalDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = total
            },
            "Project proposals retrieved successfully.");
    }

    public async Task<ServiceResult<ProposalSceneDto>> CreateSceneAsync(
        Guid proposalId,
        Guid currentUserId,
        CreateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationError = ValidateCreateSceneRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<ProposalSceneDto>.Failure(validationError);
        }

        var proposal = await _proposals.GetProposalContextAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<ProposalSceneDto>.NotFound(ProposalNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStaffAccessProposal(proposal, currentUserId, roleName))
        {
            return ServiceResult<ProposalSceneDto>.Forbidden("You do not have access to create scenes for this proposal.");
        }

        if (proposal.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<ProposalSceneDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Proposal scene can only be created for draft proposal."));
        }

        if (request.PreviewFileId.HasValue &&
            !await _proposals.FileExistsAsync(request.PreviewFileId.Value, cancellationToken))
        {
            return ServiceResult<ProposalSceneDto>.Failure(Error.NotFound(
                PreviewFileNotFoundCode,
                "Preview file not found."));
        }

        var areaValidation = await ValidateSceneAreaIdsAsync(
            request.ProjectAreaIds,
            proposal.ProjectId,
            cancellationToken);
        if (!areaValidation.IsValid)
        {
            return ServiceResult<ProposalSceneDto>.Failure(areaValidation.Error!);
        }

        var now = DateTime.UtcNow;
        var scene = new ProposalScene
        {
            SceneId = Guid.NewGuid(),
            ProposalId = proposalId,
            SceneName = NormalizeOptional(request.SceneName),
            SceneType = request.SceneType,
            MongoSceneId = null,
            PreviewFileId = request.PreviewFileId,
            VersionNo = await _proposals.CountScenesAsync(proposalId, cancellationToken) + 1,
            IsActive = true,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            AddSceneAreas(scene, areaValidation.Areas, now);
            await _proposals.AddSceneAsync(scene, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ServiceResult<ProposalSceneDto>.Created(
                ToProposalSceneDto(scene, areaValidation.Areas),
                "Proposal scene created successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<ProposalSceneListResponseDto>> GetScenesAsync(
        Guid proposalId,
        Guid currentUserId,
        ProposalSceneListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneListResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneListResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var paginationError = ValidatePagination(query.Page, query.Limit);
        if (paginationError is not null)
        {
            return ServiceResult<ProposalSceneListResponseDto>.BadRequest(paginationError);
        }

        var proposal = await _proposals.GetProposalContextAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<ProposalSceneListResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProposalContext(proposal, currentUserId, roleName))
        {
            return ServiceResult<ProposalSceneListResponseDto>.Forbidden("You do not have access to view proposal scenes.");
        }

        var repositoryQuery = new ProposalSceneListQueryReadModel
        {
            ProposalId = proposalId,
            SceneType = query.SceneType,
            IsActive = query.IsActive,
            ActiveOnly = IsCustomer(roleName),
            Page = query.Page,
            Limit = query.Limit
        };
        var scenes = await _proposals.GetScenesAsync(repositoryQuery, cancellationToken);
        var total = await _proposals.CountScenesAsync(repositoryQuery, cancellationToken);

        return ServiceResult<ProposalSceneListResponseDto>.Success(
            new ProposalSceneListResponseDto
            {
                Items = scenes.Adapt<List<ProposalSceneDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = total
            },
            "Proposal scenes retrieved successfully.");
    }

    public async Task<ServiceResult<ProposalSceneDetailDto>> GetSceneDetailAsync(
        Guid sceneId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (sceneId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneDetailDto>.BadRequest("Scene id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalSceneDetailDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var scene = await _proposals.GetSceneDetailAsync(sceneId, cancellationToken);
        if (scene is null)
        {
            return ServiceResult<ProposalSceneDetailDto>.Failure(Error.NotFound(
                "SCENE_NOT_FOUND",
                ProposalSceneNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProposalScene(scene, currentUserId, roleName))
        {
            return ServiceResult<ProposalSceneDetailDto>.Forbidden("You do not have access to view this proposal scene.");
        }

        return ServiceResult<ProposalSceneDetailDto>.Success(
            scene.Adapt<ProposalSceneDetailDto>(),
            "Proposal scene detail retrieved successfully.");
    }

    public async Task<ServiceResult<ProposalDetailDto>> GetDetailAsync(
        Guid proposalId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<ProposalDetailDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalDetailDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var proposal = await _proposals.GetDetailAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<ProposalDetailDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProposalDetail(proposal, currentUserId, roleName))
        {
            return ServiceResult<ProposalDetailDto>.Forbidden("You do not have access to view this proposal.");
        }

        return ServiceResult<ProposalDetailDto>.Success(
            proposal.Adapt<ProposalDetailDto>(),
            "Proposal detail retrieved successfully.");
    }

    public async Task<ServiceResult<PublishedProposalDto>> GetPublishedByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<PublishedProposalDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PublishedProposalDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var project = await _proposals.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<PublishedProposalDto>.Failure(Error.NotFound(
                "PROJECT_NOT_FOUND",
                "Project not found."));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || project.CustomerId != currentUserId)
        {
            return ServiceResult<PublishedProposalDto>.Forbidden("You do not have access to view this published proposal.");
        }

        var proposal = await _proposals.GetLatestPublishedByProjectAsync(projectId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<PublishedProposalDto>.Failure(Error.NotFound(
                "PUBLISHED_PROPOSAL_NOT_FOUND",
                "Published proposal not found."));
        }

        return ServiceResult<PublishedProposalDto>.Success(
            ToPublishedProposalDto(proposal),
            "Published proposal retrieved successfully.");
    }

    public async Task<ServiceResult<ProposalItemListResponseDto>> GetItemsAsync(
        Guid proposalId,
        Guid currentUserId,
        ProposalItemListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<ProposalItemListResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProposalItemListResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var paginationError = ValidatePagination(query.Page, query.Limit);
        if (paginationError is not null)
        {
            return ServiceResult<ProposalItemListResponseDto>.BadRequest(paginationError);
        }

        var proposal = await _proposals.GetProposalContextAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<ProposalItemListResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProposalContext(proposal, currentUserId, roleName))
        {
            return ServiceResult<ProposalItemListResponseDto>.Forbidden("You do not have access to view proposal items.");
        }

        var repositoryQuery = new ProposalItemListQueryReadModel
        {
            ProposalId = proposalId,
            SceneId = query.SceneId,
            Page = query.Page,
            Limit = query.Limit
        };
        var items = await _proposals.GetItemsAsync(repositoryQuery, cancellationToken);
        var total = await _proposals.CountItemsAsync(repositoryQuery, cancellationToken);
        var itemDtos = items.Adapt<List<ProposalItemSummaryDto>>();
        await PopulateSceneObjectIdsAsync(itemDtos, cancellationToken);

        return ServiceResult<ProposalItemListResponseDto>.Success(
            new ProposalItemListResponseDto
            {
                Items = itemDtos,
                Page = query.Page,
                Limit = query.Limit,
                Total = total
            },
            "Proposal items retrieved successfully.");
    }

    public async Task<ServiceResult<UpdateProposalItemResponseDto>> UpdateItemAsync(
        Guid proposalItemId,
        Guid currentUserId,
        UpdateProposalItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (proposalItemId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.BadRequest("Proposal item id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        if (request.Quantity <= 0)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Failure(Error.BadRequest(
                "INVALID_QUANTITY",
                "Quantity must be greater than 0."));
        }

        var note = NormalizeOptional(request.CustomizationNote);
        if (note?.Length > MaxCustomizationNoteLength)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.BadRequest("Customization note must not exceed 1000 characters.");
        }

        var item = await _proposals.GetItemDetailAsync(proposalItemId, cancellationToken);
        if (item is null)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Failure(Error.NotFound(
                ProposalItemNotFoundCode,
                ProposalItemNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessAssignedStaff(item.AssignedSalesId, item.AssignedDesignerId, currentUserId, roleName))
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Forbidden("You do not have access to update this proposal item.");
        }

        if (item.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Proposal item can only be updated while proposal is draft."));
        }

        var entity = await _proposals.GetItemEntityAsync(proposalItemId, cancellationToken);
        if (entity is null)
        {
            return ServiceResult<UpdateProposalItemResponseDto>.Failure(Error.NotFound(
                ProposalItemNotFoundCode,
                ProposalItemNotFoundMessage));
        }

        entity.Quantity = request.Quantity;
        entity.TotalPriceSnapshot = (entity.UnitPriceSnapshot ?? 0m) * request.Quantity;
        entity.Note = note;
        entity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UpdateProposalItemResponseDto>.Success(
            ToUpdateProposalItemDto(item, entity),
            "Proposal item updated successfully.");
    }

    public async Task<ServiceResult<DeleteProposalItemResponseDto>> DeleteItemAsync(
        Guid proposalItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (proposalItemId == Guid.Empty)
        {
            return ServiceResult<DeleteProposalItemResponseDto>.BadRequest("Proposal item id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<DeleteProposalItemResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var item = await _proposals.GetItemDetailAsync(proposalItemId, cancellationToken);
        if (item is null)
        {
            return ServiceResult<DeleteProposalItemResponseDto>.Failure(Error.NotFound(
                ProposalItemNotFoundCode,
                ProposalItemNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessAssignedStaff(item.AssignedSalesId, item.AssignedDesignerId, currentUserId, roleName))
        {
            return ServiceResult<DeleteProposalItemResponseDto>.Forbidden("You do not have access to delete this proposal item.");
        }

        if (item.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<DeleteProposalItemResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Proposal item can only be deleted while proposal is draft."));
        }

        var entity = await _proposals.GetItemEntityAsync(proposalItemId, cancellationToken);
        if (entity is null)
        {
            return ServiceResult<DeleteProposalItemResponseDto>.Failure(Error.NotFound(
                ProposalItemNotFoundCode,
                ProposalItemNotFoundMessage));
        }

        _proposals.RemoveItem(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<DeleteProposalItemResponseDto>.Success(
            new DeleteProposalItemResponseDto
            {
                ProposalItemId = proposalItemId,
                Deleted = true
            },
            "Proposal item deleted successfully.");
    }

    public async Task<ServiceResult<SyncProposalItemsFromSceneResponseDto>> SyncItemsFromSceneAsync(
        Guid proposalId,
        Guid currentUserId,
        SyncProposalItemsFromSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationErrors = ValidateSyncItemsRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.BadRequest(validationErrors);
        }

        var proposal = await _proposals.GetProposalContextAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanSyncProposalItems(proposal, currentUserId, roleName))
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Forbidden("You do not have access to sync proposal items.");
        }

        if (!IsEditableProposal(proposal.ProposalStatus))
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.BadRequest(
                ProposalNotEditableCode,
                "Proposal is not editable."));
        }

        var scene = await _proposals.GetSceneContextAsync(proposalId, request.SceneId, cancellationToken);
        if (scene is null || scene.SceneType != ProposalSceneType.ROOM_PLANNER)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.NotFound(
                ProposalSceneNotFoundCode,
                ProposalSceneNotFoundMessage));
        }

        var roomPlannerScene = await GetRoomPlannerSceneForSyncAsync(request.SceneId, cancellationToken);
        if (roomPlannerScene is null)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.NotFound(
                RoomPlannerDocumentNotFoundCode,
                "Room Planner scene not found."));
        }

        var documentError = ValidateRoomPlannerSceneForSync(proposal, scene, roomPlannerScene);
        if (documentError is not null)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(documentError);
        }

        var syncItemsResult = CreateSyncItemsFromRoomPlanner(scene, roomPlannerScene);
        if (syncItemsResult.Error is not null)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(syncItemsResult.Error);
        }

        var productVersions = await GetProductVersionsForSyncAsync(
            syncItemsResult.Items,
            scene.ProjectId,
            cancellationToken);
        if (productVersions.Count != syncItemsResult.Items.Select(item => item.ProductVersionId).Distinct().Count())
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.BadRequest(
                ProductVersionNotFoundCode,
                "One or more product versions are invalid."));
        }

        var existingItems = await _proposals.GetItemsBySceneAsync(proposalId, request.SceneId, cancellationToken);
        if (HasDuplicateExistingSceneObjectMappings(existingItems))
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.BadRequest(
                DuplicateSceneObjectMappingCode,
                "Existing proposal items contain duplicate scene object mappings."));
        }

        SyncProposalItemsFromSceneResponseDto response;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var syncResult = await UpsertProposalItemsAsync(
                syncItemsResult.Items,
                scene,
                productVersions,
                existingItems,
                now,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            response = new SyncProposalItemsFromSceneResponseDto
            {
                ProposalId = proposalId,
                SceneId = request.SceneId,
                Items = syncResult.Items,
                CreatedCount = syncResult.CreatedCount,
                UpdatedCount = syncResult.UpdatedCount,
                RemovedCount = syncResult.RemovedCount
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.InternalServerError(
                ProposalItemSyncFailedCode,
                "Proposal items could not be synced from Room Planner scene."));
        }

        try
        {
            await LinkRoomPlannerObjectsToProposalItemsAsync(request.SceneId, response.Items, cancellationToken);
        }
        catch
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.InternalServerError(
                MongoProposalItemLinkFailedCode,
                "Proposal items were synced but Room Planner scene link update failed."));
        }

        return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Success(
            response,
            "Proposal items synced from Room Planner scene successfully.");
    }

    public async Task<ServiceResult<SelectFinalProposalResponseDto>> SelectFinalAsync(
        Guid proposalId,
        Guid currentUserId,
        SelectFinalProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var normalizedNote = NormalizeOptional(request?.Note);
        if (normalizedNote?.Length > MaxCustomizationNoteLength)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.BadRequest("Selection note must not exceed 1000 characters.");
        }

        var proposal = await _proposals.GetDetailAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || proposal.CustomerId != currentUserId)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Forbidden("You do not have access to select this proposal.");
        }

        if (!IsSelectableFinalProposal(proposal.Status))
        {
            if (proposal.Status == ProposalStatus.SELECTED)
            {
                return ServiceResult<SelectFinalProposalResponseDto>.Failure(Error.BadRequest(
                    CustomizationRequestErrorCodes.ProposalAlreadySelected,
                    "Proposal has already been selected."));
            }

            return ServiceResult<SelectFinalProposalResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Only published proposals can be selected as final."));
        }

        if (await HasPendingCustomizationRequestsAsync(proposalId, cancellationToken))
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Failure(Error.BadRequest(
                CustomizationRequestErrorCodes.CustomizationRequestPending,
                "Proposal has unresolved customization requests."));
        }

        var proposalEntity = await _proposals.GetProposalEntityAsync(proposalId, cancellationToken);
        var projectEntity = await _projects.GetByIdAsync(proposal.ProjectId, cancellationToken);
        if (proposalEntity is null || projectEntity is null)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.NotFound(ProposalNotFoundMessage);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            proposalEntity.Status = ProposalStatus.SELECTED;
            proposalEntity.SelectedAt = now;
            proposalEntity.UpdatedAt = now;
            projectEntity.Status = ProjectStatus.PROPOSAL_SELECTED;
            projectEntity.UpdatedAt = now;

            await _proposals.RejectOtherActiveProposalsAsync(
                proposal.ProjectId,
                proposalId,
                now,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            await DispatchProposalFinalSelectedNotificationAsync(proposal, cancellationToken);

            return ServiceResult<SelectFinalProposalResponseDto>.Success(
                new SelectFinalProposalResponseDto
                {
                    ProposalId = proposalId,
                    ProjectId = proposal.ProjectId,
                    ProposalStatus = proposalEntity.Status,
                    ProjectStatus = projectEntity.Status,
                    SelectedAt = proposalEntity.SelectedAt
                },
                "Final proposal selected successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<RequestProposalRevisionResponseDto>> RequestRevisionAsync(
        Guid proposalId,
        Guid currentUserId,
        RequestProposalRevisionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var revisionNote = NormalizeOptional(request?.RevisionNote);
        if (string.IsNullOrWhiteSpace(revisionNote))
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.BadRequest("Revision note is required.");
        }

        if (revisionNote.Length > MaxCustomizationNoteLength)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.BadRequest("Revision note must not exceed 1000 characters.");
        }

        var proposal = await _proposals.GetDetailAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || proposal.CustomerId != currentUserId)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.Forbidden("You do not have access to request revision for this proposal.");
        }

        if (!CanRequestRevision(proposal.Status))
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Only published proposals can be requested for revision."));
        }

        var proposalEntity = await _proposals.GetProposalEntityAsync(proposalId, cancellationToken);
        if (proposalEntity is null)
        {
            return ServiceResult<RequestProposalRevisionResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var now = DateTime.UtcNow;
        proposalEntity.Status = ProposalStatus.REVISION_REQUESTED;
        proposalEntity.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProposalRevisionRequestedNotificationAsync(proposal, cancellationToken);

        return ServiceResult<RequestProposalRevisionResponseDto>.Success(
            new RequestProposalRevisionResponseDto
            {
                ProposalId = proposalId,
                ProjectId = proposal.ProjectId,
                ProposalStatus = proposalEntity.Status,
                RevisionNote = revisionNote,
                RequestedAt = now
            },
            "Proposal revision requested successfully.");
    }

    public async Task<ServiceResult<PublishProposalResponseDto>> PublishAsync(
        Guid proposalId,
        Guid currentUserId,
        PublishProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<PublishProposalResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PublishProposalResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var proposal = await _proposals.GetDetailAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<PublishProposalResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStaffAccessProposal(proposal, currentUserId, roleName))
        {
            return ServiceResult<PublishProposalResponseDto>.Forbidden("You do not have access to publish this proposal.");
        }

        if (proposal.Status != ProposalStatus.DRAFT)
        {
            return ServiceResult<PublishProposalResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Only draft proposals can be published."));
        }

        if (!await _proposals.HasActiveSceneAsync(proposalId, cancellationToken))
        {
            return ServiceResult<PublishProposalResponseDto>.Failure(Error.BadRequest(
                "PROPOSAL_SCENE_REQUIRED",
                "Proposal must have at least one active scene before publishing."));
        }

        var proposalEntity = await _proposals.GetProposalEntityAsync(proposalId, cancellationToken);
        var projectEntity = await _projects.GetByIdAsync(proposal.ProjectId, cancellationToken);
        if (proposalEntity is null || projectEntity is null)
        {
            return ServiceResult<PublishProposalResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            proposalEntity.Status = ProposalStatus.PUBLISHED;
            proposalEntity.PublishedAt = now;
            proposalEntity.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            await DispatchProposalPublishedNotificationAsync(proposal, cancellationToken);

            return ServiceResult<PublishProposalResponseDto>.Success(
                new PublishProposalResponseDto
                {
                    ProposalId = proposalId,
                    ProjectId = proposal.ProjectId,
                    ProposalStatus = proposalEntity.Status,
                    ProjectStatus = projectEntity.Status,
                    PublishedAt = now
                },
                "Proposal published for customer review successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<UpdateProposalResponseDto>> UpdateAsync(
        Guid proposalId,
        Guid currentUserId,
        UpdateProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalResponseDto>.BadRequest(ProposalIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationErrors = ValidateUpdateProposalRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<UpdateProposalResponseDto>.BadRequest(validationErrors);
        }

        var proposal = await _proposals.GetProposalContextAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return ServiceResult<UpdateProposalResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStaffAccessProposal(proposal, currentUserId, roleName))
        {
            return ServiceResult<UpdateProposalResponseDto>.Forbidden("You do not have access to update this proposal.");
        }

        if (proposal.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<UpdateProposalResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Only draft proposals can be updated."));
        }

        var proposalEntity = await _proposals.GetProposalEntityAsync(proposalId, cancellationToken);
        if (proposalEntity is null)
        {
            return ServiceResult<UpdateProposalResponseDto>.Failure(Error.NotFound(
                ProposalNotFoundCode,
                ProposalNotFoundMessage));
        }

        var now = DateTime.UtcNow;
        if (request.ProposalName is not null)
        {
            proposalEntity.ProposalName = request.ProposalName.Trim();
        }

        if (request.Description is not null)
        {
            proposalEntity.Description = NormalizeOptional(request.Description);
        }

        proposalEntity.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<UpdateProposalResponseDto>.Success(
            new UpdateProposalResponseDto
            {
                ProposalId = proposalId,
                ProjectId = proposal.ProjectId,
                ProposalName = proposalEntity.ProposalName,
                Description = proposalEntity.Description,
                VersionNo = proposalEntity.VersionNo,
                Status = proposalEntity.Status,
                UpdatedAt = now
            },
            "Proposal updated successfully.");
    }

    public async Task<ServiceResult<UpdateProposalSceneResponseDto>> UpdateSceneAsync(
        Guid sceneId,
        Guid currentUserId,
        UpdateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (sceneId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.BadRequest("Scene id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationErrors = ValidateUpdateSceneRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.BadRequest(validationErrors);
        }

        var sceneContext = await _proposals.GetSceneContextBySceneIdAsync(sceneId, cancellationToken);
        if (sceneContext is null)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Failure(Error.NotFound(
                ProposalSceneNotFoundCode,
                ProposalSceneNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStaffAccessProposalScene(sceneContext, currentUserId, roleName))
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Forbidden("You do not have access to update this proposal scene.");
        }

        if (sceneContext.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Failure(Error.BadRequest(
                InvalidProposalStatusCode,
                "Proposal scene metadata can only be updated for draft proposal."));
        }

        if (request.PreviewFileId.HasValue &&
            !await _proposals.FileExistsAsync(request.PreviewFileId.Value, cancellationToken))
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Failure(Error.NotFound(
                PreviewFileNotFoundCode,
                "Preview file not found."));
        }

        var areaValidation = request.ProjectAreaIds is null
            ? SceneAreaValidationResult.Valid(sceneContext.SceneAreas)
            : await ValidateSceneAreaIdsAsync(request.ProjectAreaIds, sceneContext.ProjectId, cancellationToken);
        if (!areaValidation.IsValid)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Failure(areaValidation.Error!);
        }

        var scene = await _proposals.GetSceneEntityAsync(sceneId, cancellationToken);
        if (scene is null)
        {
            return ServiceResult<UpdateProposalSceneResponseDto>.Failure(Error.NotFound(
                ProposalSceneNotFoundCode,
                ProposalSceneNotFoundMessage));
        }

        var now = DateTime.UtcNow;
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            if (request.SceneName is not null)
            {
                scene.SceneName = NormalizeOptional(request.SceneName);
            }

            scene.PreviewFileId = request.PreviewFileId;
            if (request.IsActive.HasValue)
            {
                scene.IsActive = request.IsActive;
            }

            scene.UpdatedAt = now;
            if (request.ProjectAreaIds is not null)
            {
                await _proposals.ReplaceSceneAreasAsync(scene.SceneId, request.ProjectAreaIds.ToList(), now, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ServiceResult<UpdateProposalSceneResponseDto>.Success(
                ToUpdateSceneResponse(scene, areaValidation.Areas, now),
                "Proposal scene updated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static List<string> ValidateCreateRequest(CreateProposalRequestDto request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ProposalName))
        {
            errors.Add("Proposal name is required.");
        }
        else if (request.ProposalName.Trim().Length > MaxProposalNameLength)
        {
            errors.Add("Proposal name must not exceed 150 characters.");
        }

        if (NormalizeOptional(request.Description)?.Length > MaxDescriptionLength)
        {
            errors.Add("Proposal description must not exceed 1000 characters.");
        }

        return errors;
    }

    private static Error? ValidateCreateSceneRequest(CreateProposalSceneRequestDto request)
    {
        if (request is null)
        {
            return Error.BadRequest(SceneNameRequiredCode, "Create proposal scene request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SceneName))
        {
            return Error.BadRequest(SceneNameRequiredCode, "Scene name is required.");
        }

        if (request.SceneName.Trim().Length > MaxSceneNameLength)
        {
            return Error.BadRequest(SceneNameRequiredCode, "Scene name must not exceed 150 characters.");
        }

        if (request.SceneType is null)
        {
            return Error.BadRequest(SceneTypeRequiredCode, "Scene type is required.");
        }

        if (request.SceneType != ProposalSceneType.ROOM_PLANNER)
        {
            return Error.BadRequest(
                SceneTypeRequiredCode,
                "Only ROOM_PLANNER scene type is supported for new proposal scenes.");
        }

        return null;
    }

    private static List<string> ValidateSyncItemsRequest(SyncProposalItemsFromSceneRequestDto request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Sync request is required.");
            return errors;
        }

        if (request.SceneId == Guid.Empty)
        {
            errors.Add("Scene id is required.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateProposalRequest(UpdateProposalRequestDto request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Update request is required.");
            return errors;
        }

        if (request.ProposalName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.ProposalName))
            {
                errors.Add("Proposal name is required.");
            }
            else if (request.ProposalName.Trim().Length > MaxProposalNameLength)
            {
                errors.Add("Proposal name must not exceed 150 characters.");
            }
        }

        if (NormalizeOptional(request.Description)?.Length > MaxDescriptionLength)
        {
            errors.Add("Proposal description must not exceed 1000 characters.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateSceneRequest(UpdateProposalSceneRequestDto request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Update scene request is required.");
            return errors;
        }

        if (request.SceneName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SceneName))
            {
                errors.Add("Scene name is required.");
            }
            else if (request.SceneName.Trim().Length > MaxSceneNameLength)
            {
                errors.Add("Scene name must not exceed 150 characters.");
            }
        }

        return errors;
    }

    private static string? ValidatePagination(int page, int limit)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (limit is < 1 or > MaxPageSize)
        {
            return "Limit must be between 1 and 100.";
        }

        return null;
    }

    private static bool CanViewProjectProposals(
        ProposalProjectAccessReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return project.CustomerId == currentUserId;
        }

        return CanStaffAccessProject(project, currentUserId, roleName);
    }

    private static bool CanViewProposalDetail(
        ProposalDetailReadModel proposal,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return proposal.CustomerId == currentUserId && IsCustomerVisible(proposal.Status);
        }

        if (IsSales(roleName))
        {
            return proposal.AssignedSalesId == currentUserId;
        }

        return IsDesigner(roleName) && proposal.AssignedDesignerId == currentUserId;
    }

    private static bool CanViewProposalContext(
        ProposalContextReadModel proposal,
        Guid currentUserId,
        string? roleName)
    {
        if (IsCustomer(roleName))
        {
            return proposal.CustomerId == currentUserId && IsCustomerVisible(proposal.ProposalStatus);
        }

        return CanStaffAccessProposal(proposal, currentUserId, roleName);
    }

    private static bool CanViewProposalScene(
        ProposalSceneDetailReadModel scene,
        Guid currentUserId,
        string? roleName)
    {
        if (IsCustomer(roleName))
        {
            return scene.CustomerId == currentUserId && IsCustomerVisible(scene.ProposalStatus);
        }

        return CanAccessAssignedStaff(
            scene.AssignedSalesId,
            scene.AssignedDesignerId,
            currentUserId,
            roleName);
    }

    private static bool CanStaffAccessProject(
        ProposalProjectAccessReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsSales(roleName))
        {
            return project.AssignedSalesId == currentUserId;
        }

        return IsDesigner(roleName) && project.AssignedDesignerId == currentUserId;
    }

    private static bool CanStaffAccessProposal(
        ProposalContextReadModel proposal,
        Guid currentUserId,
        string? roleName)
    {
        return CanAccessAssignedStaff(
            proposal.AssignedSalesId,
            proposal.AssignedDesignerId,
            currentUserId,
            roleName);
    }

    private static bool CanStaffAccessProposal(
        ProposalDetailReadModel proposal,
        Guid currentUserId,
        string? roleName)
    {
        return CanAccessAssignedStaff(
            proposal.AssignedSalesId,
            proposal.AssignedDesignerId,
            currentUserId,
            roleName);
    }

    private static bool CanStaffAccessProposalScene(
        ProposalSceneContextReadModel scene,
        Guid currentUserId,
        string? roleName)
    {
        return CanAccessAssignedStaff(
            scene.AssignedSalesId,
            scene.AssignedDesignerId,
            currentUserId,
            roleName);
    }

    private static bool CanSyncProposalItems(
        ProposalContextReadModel proposal,
        Guid currentUserId,
        string? roleName)
    {
        return IsAdmin(roleName) || (IsDesigner(roleName) && proposal.AssignedDesignerId == currentUserId);
    }

    private static bool IsEditableProposal(ProposalStatus? status)
    {
        return status is ProposalStatus.DRAFT or ProposalStatus.REVISION_REQUESTED;
    }

    private static bool CanAccessAssignedStaff(
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsSales(roleName))
        {
            return assignedSalesId == currentUserId;
        }

        return IsDesigner(roleName) && assignedDesignerId == currentUserId;
    }

    private async Task<RoomPlannerSceneDocument?> GetRoomPlannerSceneForSyncAsync(
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        return _roomPlannerScenes is null
            ? null
            : await _roomPlannerScenes.GetBySqlSceneIdAsync(sceneId, cancellationToken);
    }

    private static Error? ValidateRoomPlannerSceneForSync(
        ProposalContextReadModel proposal,
        ProposalSceneContextReadModel scene,
        RoomPlannerSceneDocument roomPlannerScene)
    {
        if (roomPlannerScene.SqlSceneId != scene.SceneId ||
            roomPlannerScene.ProposalId != scene.ProposalId ||
            roomPlannerScene.ProjectId != proposal.ProjectId)
        {
            return Error.BadRequest(SceneProposalMismatchCode, "Room Planner scene does not match proposal scene.");
        }

        if (roomPlannerScene.BlueprintLayout is null)
        {
            return Error.BadRequest(InvalidObjectFloorReferenceCode, "Room Planner scene blueprint layout is required.");
        }

        return null;
    }

    private static SceneSyncItemsResult CreateSyncItemsFromRoomPlanner(
        ProposalSceneContextReadModel scene,
        RoomPlannerSceneDocument roomPlannerScene)
    {
        var floorAreaIds = roomPlannerScene.BlueprintLayout!.Floors
            .Where(floor => !string.IsNullOrWhiteSpace(floor.Id))
            .GroupBy(floor => floor.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().ProjectAreaId,
                StringComparer.Ordinal);
        var sceneAreaIds = scene.GetProjectAreaIds().ToHashSet();
        var syncItems = new List<RoomPlannerSceneSyncItem>();
        var sceneObjectIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sceneObject in GetEligibleRoomPlannerObjects(roomPlannerScene.Objects))
        {
            var sceneObjectId = NormalizeOptional(sceneObject.ObjectId);
            if (sceneObjectId is null)
            {
                return SceneSyncItemsResult.Invalid(Error.BadRequest(
                    DuplicateSceneObjectMappingCode,
                    "Room Planner scene object id is required."));
            }

            if (!sceneObjectIds.Add(sceneObjectId))
            {
                return SceneSyncItemsResult.Invalid(Error.BadRequest(
                    DuplicateSceneObjectMappingCode,
                    "Room Planner scene object ids must be unique."));
            }

            var floorId = NormalizeOptional(sceneObject.FloorId);
            if (floorId is null || !floorAreaIds.TryGetValue(floorId, out var projectAreaId))
            {
                return SceneSyncItemsResult.Invalid(Error.BadRequest(
                    InvalidObjectFloorReferenceCode,
                    "Scene object references a nonexistent blueprint floor."));
            }

            if (!sceneAreaIds.Contains(projectAreaId))
            {
                return SceneSyncItemsResult.Invalid(Error.BadRequest(
                    SceneAreaMappingNotFoundCode,
                    "Scene object floor is not mapped to this proposal scene."));
            }

            syncItems.Add(new RoomPlannerSceneSyncItem(
                sceneObjectId,
                floorId,
                projectAreaId,
                sceneObject.ProductVersionId));
        }

        return SceneSyncItemsResult.Valid(syncItems);
    }

    private static IEnumerable<RoomPlannerObjectDocument> GetEligibleRoomPlannerObjects(
        IEnumerable<RoomPlannerObjectDocument> sceneObjects) =>
        sceneObjects.Where(sceneObject =>
            string.Equals(sceneObject.ObjectType, "FURNITURE", StringComparison.OrdinalIgnoreCase) &&
            sceneObject.ProductVersionId != Guid.Empty);

    private async Task<Dictionary<Guid, Infrastructure.ReadModels.Products.ProductVersionDetailReadModel>> GetProductVersionsForSyncAsync(
        IEnumerable<RoomPlannerSceneSyncItem> items,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var productVersionIds = items
            .Select(item => item.ProductVersionId)
            .Where(productVersionId => productVersionId != Guid.Empty)
            .Distinct()
            .ToList();
        var productVersions = await _productVersions.GetValidDetailsAsync(
            productVersionIds,
            projectId,
            cancellationToken);

        return productVersions.ToDictionary(version => version.ProductVersionId);
    }

    private async Task PopulateSceneObjectIdsAsync(
        List<ProposalItemSummaryDto> items,
        CancellationToken cancellationToken)
    {
        if (_roomPlannerScenes is null || items.Count == 0)
        {
            return;
        }

        var sceneObjectIds = await GetSceneObjectIdsByProposalItemAsync(items, cancellationToken);
        foreach (var item in items)
        {
            if (sceneObjectIds.TryGetValue(item.ProposalItemId, out var sceneObjectId))
            {
                item.SceneObjectId = sceneObjectId;
            }
        }
    }

    private async Task<Dictionary<Guid, string>> GetSceneObjectIdsByProposalItemAsync(
        List<ProposalItemSummaryDto> items,
        CancellationToken cancellationToken)
    {
        var sceneObjectIds = new Dictionary<Guid, string>();
        var itemsByScene = items
            .Where(item => item.SceneId.HasValue)
            .GroupBy(item => item.SceneId!.Value);

        foreach (var sceneItems in itemsByScene)
        {
            var scene = await _roomPlannerScenes!.GetBySqlSceneIdAsync(sceneItems.Key, cancellationToken);
            if (scene is null)
            {
                continue;
            }

            AddExactSceneObjectMatches(scene.Objects, sceneObjectIds);
            AddSingleObjectFallback(sceneItems, scene.Objects, sceneObjectIds);
        }

        return sceneObjectIds;
    }

    private static void AddExactSceneObjectMatches(
        IEnumerable<RoomPlannerObjectDocument> sceneObjects,
        Dictionary<Guid, string> sceneObjectIds)
    {
        foreach (var sceneObject in sceneObjects)
        {
            if (sceneObject.ProposalItemId.HasValue && !string.IsNullOrWhiteSpace(sceneObject.ObjectId))
            {
                sceneObjectIds[sceneObject.ProposalItemId.Value] = sceneObject.ObjectId;
            }
        }
    }

    private static void AddSingleObjectFallback(
        IEnumerable<ProposalItemSummaryDto> sceneItems,
        IEnumerable<RoomPlannerObjectDocument> sceneObjects,
        Dictionary<Guid, string> sceneObjectIds)
    {
        var unmatchedItems = sceneItems
            .Where(item => !sceneObjectIds.ContainsKey(item.ProposalItemId))
            .ToList();
        var objectIds = sceneObjects
            .Where(sceneObject => !string.IsNullOrWhiteSpace(sceneObject.ObjectId))
            .Select(sceneObject => sceneObject.ObjectId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unmatchedItems.Count == 1 && objectIds.Count == 1)
        {
            sceneObjectIds[unmatchedItems[0].ProposalItemId] = objectIds[0];
        }
    }

    private async Task LinkRoomPlannerObjectsToProposalItemsAsync(
        Guid sceneId,
        IEnumerable<SyncedProposalItemDto> syncedItems,
        CancellationToken cancellationToken)
    {
        if (_roomPlannerScenes is null)
        {
            return;
        }

        var scene = await _roomPlannerScenes.GetBySqlSceneIdAsync(sceneId, cancellationToken);
        if (scene is null)
        {
            return;
        }

        var proposalItemIdsByObjectId = syncedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.SceneObjectId))
            .GroupBy(item => item.SceneObjectId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().ProposalItemId,
                StringComparer.Ordinal);
        var hasChanges = false;

        foreach (var sceneObject in scene.Objects)
        {
            if (!string.IsNullOrWhiteSpace(sceneObject.ObjectId) &&
                proposalItemIdsByObjectId.TryGetValue(sceneObject.ObjectId, out var proposalItemId) &&
                sceneObject.ProposalItemId != proposalItemId)
            {
                sceneObject.ProposalItemId = proposalItemId;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _roomPlannerScenes.UpsertBySqlSceneIdAsync(scene, cancellationToken);
        }
    }

    private async Task<ProposalItemSyncResult> UpsertProposalItemsAsync(
        IReadOnlyList<RoomPlannerSceneSyncItem> syncItems,
        ProposalSceneContextReadModel scene,
        Dictionary<Guid, Infrastructure.ReadModels.Products.ProductVersionDetailReadModel> productVersions,
        IReadOnlyList<ProposalItem> existingItems,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var syncedItems = new List<SyncedProposalItemDto>();
        var existingItemsBySceneObjectId = existingItems
            .Where(item => !string.IsNullOrWhiteSpace(item.SceneObjectId))
            .GroupBy(item => item.SceneObjectId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var createdCount = 0;
        var updatedCount = 0;
        foreach (var syncItem in syncItems)
        {
            var productVersion = productVersions[syncItem.ProductVersionId];
            var proposalItem = FindExistingProposalItem(existingItemsBySceneObjectId, syncItem.SceneObjectId);
            if (proposalItem is null)
            {
                proposalItem = await CreateProposalItemAsync(scene, syncItem, now, cancellationToken);
                existingItemsBySceneObjectId[syncItem.SceneObjectId] = proposalItem;
                createdCount++;
            }
            else
            {
                updatedCount++;
            }

            ApplyProposalItemSnapshot(proposalItem, productVersion, syncItem, now);
            syncedItems.Add(ToSyncedItemDto(proposalItem, productVersion, syncItem.FloorId));
        }

        return new ProposalItemSyncResult(syncedItems, createdCount, updatedCount, 0);
    }

    private static bool HasDuplicateExistingSceneObjectMappings(IEnumerable<ProposalItem> existingItems) =>
        existingItems
            .Where(item => !string.IsNullOrWhiteSpace(item.SceneObjectId))
            .GroupBy(item => item.SceneObjectId!, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

    private async Task<ProposalItem> CreateProposalItemAsync(
        ProposalSceneContextReadModel scene,
        RoomPlannerSceneSyncItem syncItem,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var proposalItem = new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = scene.ProposalId,
            SceneId = scene.SceneId,
            SceneObjectId = syncItem.SceneObjectId,
            ProjectAreaId = syncItem.ProjectAreaId,
            ProductVersionId = syncItem.ProductVersionId,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _proposals.AddItemAsync(proposalItem, cancellationToken);
        return proposalItem;
    }

    private static ProposalItem? FindExistingProposalItem(
        IReadOnlyDictionary<string, ProposalItem> existingItemsBySceneObjectId,
        string? sceneObjectId)
    {
        return string.IsNullOrWhiteSpace(sceneObjectId)
            ? null
            : existingItemsBySceneObjectId.GetValueOrDefault(sceneObjectId);
    }

    private static void ApplyProposalItemSnapshot(
        ProposalItem proposalItem,
        Infrastructure.ReadModels.Products.ProductVersionDetailReadModel productVersion,
        RoomPlannerSceneSyncItem syncItem,
        DateTime now)
    {
        const int QuantityPerSceneObject = 1;
        var unitPrice = productVersion.EstimatedPrice ?? 0m;
        proposalItem.ProjectAreaId = syncItem.ProjectAreaId;
        proposalItem.ProductVersionId = syncItem.ProductVersionId;
        proposalItem.ItemName = productVersion.ProductName ?? productVersion.VersionName;
        proposalItem.ItemType = productVersion.VersionType?.ToString();
        proposalItem.Width = productVersion.Width;
        proposalItem.Height = productVersion.Height;
        proposalItem.Depth = productVersion.Depth;
        proposalItem.Material = productVersion.Material;
        proposalItem.Color = productVersion.Color;
        proposalItem.Quantity = QuantityPerSceneObject;
        proposalItem.UnitPriceSnapshot = unitPrice;
        proposalItem.TotalPriceSnapshot = unitPrice * QuantityPerSceneObject;
        proposalItem.Note = null;
        proposalItem.IsCustomized = !string.IsNullOrWhiteSpace(proposalItem.Note);
        proposalItem.UpdatedAt = now;
    }

    private static SyncedProposalItemDto ToSyncedItemDto(
        ProposalItem proposalItem,
        Infrastructure.ReadModels.Products.ProductVersionDetailReadModel productVersion,
        string floorId)
    {
        return new SyncedProposalItemDto
        {
            ProposalItemId = proposalItem.ProposalItemId,
            ProjectAreaId = proposalItem.ProjectAreaId,
            FloorId = floorId,
            SceneObjectId = proposalItem.SceneObjectId,
            ProductVersionId = proposalItem.ProductVersionId,
            ProductNameSnapshot = proposalItem.ItemName,
            VersionNameSnapshot = productVersion.VersionName,
            Quantity = proposalItem.Quantity,
            UnitPriceSnapshot = proposalItem.UnitPriceSnapshot,
            TotalPriceSnapshot = proposalItem.TotalPriceSnapshot,
            SubtotalAmount = proposalItem.TotalPriceSnapshot,
            CustomizationNote = proposalItem.Note
        };
    }

    private static UpdateProposalItemResponseDto ToUpdateProposalItemDto(
        ProposalItemDetailReadModel item,
        ProposalItem entity)
    {
        return new UpdateProposalItemResponseDto
        {
            ProposalItemId = entity.ProposalItemId,
            ProposalId = entity.ProposalId,
            SceneId = entity.SceneId,
            SceneObjectId = item.SceneObjectId,
            ProjectAreaId = item.ProjectAreaId,
            ProjectAreaName = item.ProjectAreaName,
            FloorNumber = item.FloorNumber,
            ProductVersionId = entity.ProductVersionId,
            ProductNameSnapshot = entity.ItemName,
            VersionNameSnapshot = item.VersionNameSnapshot,
            MaterialSnapshot = entity.Material,
            ColorSnapshot = entity.Color,
            WidthSnapshot = entity.Width,
            HeightSnapshot = entity.Height,
            DepthSnapshot = entity.Depth,
            DimensionUnit = item.DimensionUnit,
            Quantity = entity.Quantity,
            UnitPriceSnapshot = entity.UnitPriceSnapshot,
            SubtotalAmount = entity.TotalPriceSnapshot,
            CustomizationNote = entity.Note,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static bool IsSelectableFinalProposal(ProposalStatus? status)
    {
        return status is ProposalStatus.PUBLISHED;
    }

    private async Task<SceneAreaValidationResult> ValidateSceneAreaIdsAsync(
        List<Guid>? projectAreaIds,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectAreaIds is null || projectAreaIds.Count == 0)
        {
            return SceneAreaValidationResult.Invalid(Error.BadRequest(
                RoomPlannerAreaRequiredCode,
                "At least one project area is required for a Room Planner scene."));
        }

        if (projectAreaIds.Any(id => id == Guid.Empty))
        {
            return SceneAreaValidationResult.Invalid(Error.BadRequest(
                ProjectAreaNotFoundCode,
                "Project area not found."));
        }

        if (projectAreaIds.Distinct().Count() != projectAreaIds.Count)
        {
            return SceneAreaValidationResult.Invalid(Error.BadRequest(
                DuplicateProjectAreaIdCode,
                "Duplicate project area id is not allowed."));
        }

        var areas = await _proposals.GetProjectAreasByIdsAsync(projectAreaIds, cancellationToken);
        var areasById = areas.ToDictionary(area => area.ProjectAreaId);
        if (areas.Count != projectAreaIds.Count)
        {
            return SceneAreaValidationResult.Invalid(Error.NotFound(
                ProjectAreaNotFoundCode,
                "One or more project areas were not found."));
        }

        foreach (var projectAreaId in projectAreaIds)
        {
            var area = areasById[projectAreaId];
            var error = ValidateProjectArea(area, projectId);
            if (error is not null)
            {
                return SceneAreaValidationResult.Invalid(error);
            }
        }

        return SceneAreaValidationResult.Valid(projectAreaIds
            .Select((projectAreaId, index) => ToSceneAreaReadModel(areasById[projectAreaId], index))
            .ToList());
    }

    private static Error? ValidateProjectArea(
        ProposalProjectAreaReadModel area,
        Guid projectId)
    {
        if (area.ProjectId != projectId)
        {
            return Error.BadRequest(
                ProjectAreaProjectMismatchCode,
                "Project area does not belong to the same project.");
        }

        if (area.Status == ProjectAreaStatus.CANCELLED)
        {
            return Error.BadRequest(
                ProjectAreaCancelledCode,
                "Cancelled project area cannot be used for a Room Planner scene.");
        }

        if (area.AreaType != ProjectAreaType.FLOOR)
        {
            return Error.BadRequest(
                ProjectAreaTypeNotSupportedCode,
                "Only FLOOR project areas are supported for Room Planner scenes.");
        }

        return null;
    }

    private static ProposalSceneAreaReadModel ToSceneAreaReadModel(
        ProposalProjectAreaReadModel area,
        int sortOrder)
    {
        return new ProposalSceneAreaReadModel
        {
            ProposalSceneAreaId = Guid.Empty,
            ProjectAreaId = area.ProjectAreaId,
            AreaName = area.AreaName,
            AreaType = area.AreaType,
            FloorNumber = area.FloorNumber,
            SortOrder = sortOrder,
            Status = area.Status
        };
    }

    private static void AddSceneAreas(
        ProposalScene scene,
        IReadOnlyList<ProposalSceneAreaReadModel> areas,
        DateTime now)
    {
        foreach (var area in areas)
        {
            scene.SceneAreas.Add(new ProposalSceneArea
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = scene.SceneId,
                ProjectAreaId = area.ProjectAreaId,
                SortOrder = area.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static ProposalSceneDto ToProposalSceneDto(
        ProposalScene scene,
        IReadOnlyList<ProposalSceneAreaReadModel> areas)
    {
        var dto = scene.Adapt<ProposalSceneDto>();
        dto.Areas = ToSceneAreaDtos(areas);
        return dto;
    }

    private static UpdateProposalSceneResponseDto ToUpdateSceneResponse(
        ProposalScene scene,
        IReadOnlyList<ProposalSceneAreaReadModel> areas,
        DateTime updatedAt)
    {
        return new UpdateProposalSceneResponseDto
        {
            SceneId = scene.SceneId,
            ProposalId = scene.ProposalId,
            SceneName = scene.SceneName,
            Areas = ToSceneAreaDtos(areas),
            SceneType = scene.SceneType,
            MongoSceneId = scene.MongoSceneId,
            PreviewFileId = scene.PreviewFileId,
            IsActive = scene.IsActive,
            UpdatedAt = updatedAt
        };
    }

    private static List<FurniSpace.Shared.DTOs.Proposals.ProposalSceneAreaDto> ToSceneAreaDtos(
        IReadOnlyList<ProposalSceneAreaReadModel> areas)
    {
        return areas
            .Select(area => new FurniSpace.Shared.DTOs.Proposals.ProposalSceneAreaDto
            {
                ProjectAreaId = area.ProjectAreaId,
                AreaName = area.AreaName,
                AreaType = area.AreaType?.ToString(),
                FloorNumber = area.FloorNumber,
                SortOrder = area.SortOrder,
                Status = area.Status?.ToString()
            })
            .ToList();
    }

    private sealed record SceneAreaValidationResult(
        bool IsValid,
        Error? Error,
        IReadOnlyList<ProposalSceneAreaReadModel> Areas)
    {
        public static SceneAreaValidationResult Valid(IReadOnlyList<ProposalSceneAreaReadModel> areas) =>
            new(true, null, areas);

        public static SceneAreaValidationResult Invalid(Error error) =>
            new(false, error, []);
    }

    private Task<bool> HasPendingCustomizationRequestsAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        return _customizationRequests?.HasPendingForProposalAsync(proposalId, cancellationToken)
            ?? Task.FromResult(false);
    }

    private static PublishedProposalDto ToPublishedProposalDto(ProposalDetailReadModel proposal)
    {
        var dto = proposal.Adapt<PublishedProposalDto>();
        dto.Scenes = proposal.Scenes
            .Select(scene => new PublishedProposalSceneDto
            {
                SceneId = scene.SceneId,
                SceneName = scene.SceneName,
                SceneType = scene.SceneType,
                PreviewFileUrl = scene.PreviewFileUrl,
                RoomPlannerUrl = $"/proposal-scenes/{scene.SceneId}/room-planner"
            })
            .ToList();
        dto.Items = proposal.Items.Adapt<List<ProposalItemSummaryDto>>();
        return dto;
    }

    private static bool CanRequestRevision(ProposalStatus? status)
    {
        return status is ProposalStatus.PUBLISHED;
    }

    private async Task DispatchProposalFinalSelectedNotificationAsync(
        ProposalDetailReadModel proposal,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var receiverIds = GetAssignedStaffReceiverIds(proposal);
        if (receiverIds.Count == 0)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProposalFinalSelected,
                new Dictionary<string, string>
                {
                    ["ProposalName"] = proposal.ProposalName
                },
                receiverIds,
                projectId: proposal.ProjectId,
                referenceType: "PROPOSAL",
                referenceId: proposal.ProposalId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to dispatch final proposal selected notification for proposal {ProposalId}",
                proposal.ProposalId);
        }
    }

    private async Task DispatchProposalPublishedNotificationAsync(
        ProposalDetailReadModel proposal,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProposalPublished,
                new Dictionary<string, string>
                {
                    ["ProposalName"] = proposal.ProposalName
                },
                [proposal.CustomerId],
                projectId: proposal.ProjectId,
                referenceType: "PROPOSAL",
                referenceId: proposal.ProposalId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to dispatch proposal published notification for proposal {ProposalId}",
                proposal.ProposalId);
        }
    }

    private async Task DispatchProposalRevisionRequestedNotificationAsync(
        ProposalDetailReadModel proposal,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var receiverIds = GetAssignedStaffReceiverIds(proposal);
        if (receiverIds.Count == 0)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProposalRevisionRequested,
                new Dictionary<string, string>
                {
                    ["ProposalName"] = proposal.ProposalName
                },
                receiverIds,
                projectId: proposal.ProjectId,
                referenceType: "PROPOSAL",
                referenceId: proposal.ProposalId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to dispatch proposal revision requested notification for proposal {ProposalId}",
                proposal.ProposalId);
        }
    }

    private static List<Guid> GetAssignedStaffReceiverIds(ProposalDetailReadModel proposal)
    {
        return new[] { proposal.AssignedSalesId, proposal.AssignedDesignerId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static bool IsProposalStaff(string? roleName)
    {
        return IsAdmin(roleName) || IsSales(roleName) || IsDesigner(roleName);
    }

    private static bool IsCustomerVisible(ProposalStatus? status)
    {
        return status.HasValue && CustomerVisibleStatuses.Contains(status.Value);
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSales(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class ProposalItemSyncResult
    {
        public ProposalItemSyncResult(
            List<SyncedProposalItemDto> items,
            int createdCount,
            int updatedCount,
            int removedCount)
        {
            Items = items;
            CreatedCount = createdCount;
            UpdatedCount = updatedCount;
            RemovedCount = removedCount;
        }

        public List<SyncedProposalItemDto> Items { get; }
        public int CreatedCount { get; }
        public int UpdatedCount { get; }
        public int RemovedCount { get; }
    }

    private sealed record RoomPlannerSceneSyncItem(
        string SceneObjectId,
        string FloorId,
        Guid ProjectAreaId,
        Guid ProductVersionId);

    private sealed record SceneSyncItemsResult(
        IReadOnlyList<RoomPlannerSceneSyncItem> Items,
        Error? Error)
    {
        public static SceneSyncItemsResult Valid(IReadOnlyList<RoomPlannerSceneSyncItem> items) =>
            new(items, null);

        public static SceneSyncItemsResult Invalid(Error error) => new([], error);
    }
}

