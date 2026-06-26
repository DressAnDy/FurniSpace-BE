using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Proposals;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Proposals;

public sealed class ProposalService : IProposalService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const int MaxPageSize = 100;
    private const int MaxProposalNameLength = 150;
    private const int MaxDescriptionLength = 1000;
    private const int MaxSceneNameLength = 150;
    private const int MaxCustomizationNoteLength = 1000;
    private const string AuthenticatedAccountIdRequiredMessage = "Authenticated account id is required.";
    private const string ProjectIdRequiredMessage = "Project id is required.";
    private const string ProposalIdRequiredMessage = "Proposal id is required.";
    private const string ProposalNotFoundMessage = "Proposal not found.";

    private static readonly ProposalStatus[] CustomerVisibleStatuses =
    [
        ProposalStatus.PUBLISHED,
        ProposalStatus.VIEWED,
        ProposalStatus.SELECTED,
        ProposalStatus.REVISION_REQUESTED
    ];

    private readonly IProposalRepository _proposals;
    private readonly IProjectRepository _projects;
    private readonly IProductVersionRepository _productVersions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<ProposalService>? _logger;

    public ProposalService(
        IProposalRepository proposals,
        IProjectRepository projects,
        IProductVersionRepository productVersions,
        IUnitOfWork unitOfWork,
        INotificationDispatcher? notifications = null,
        ILogger<ProposalService>? logger = null)
    {
        _proposals = proposals;
        _projects = projects;
        _productVersions = productVersions;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _logger = logger;
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

        if (project.ProjectStatus != ProjectStatus.PROPOSAL_DRAFTING)
        {
            return ServiceResult<ProposalDto>.Failure(Error.BadRequest(
                "INVALID_PROJECT_STATUS",
                "Proposal can only be created when project status is PROPOSAL_DRAFTING."));
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

        var validationErrors = ValidateCreateSceneRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProposalSceneDto>.BadRequest(validationErrors);
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
                "INVALID_PROPOSAL_STATUS",
                "Proposal scene can only be created for draft proposal."));
        }

        var now = DateTime.UtcNow;
        var scene = new ProposalScene
        {
            SceneId = Guid.NewGuid(),
            ProposalId = proposalId,
            ProjectAreaId = request.ProjectAreaId,
            SceneName = NormalizeOptional(request.SceneName),
            SceneType = request.SceneType,
            MongoSceneId = NormalizeOptional(request.MongoSceneId),
            PreviewFileId = request.PreviewFileId,
            VersionNo = await _proposals.CountScenesAsync(proposalId, cancellationToken) + 1,
            IsActive = true,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _proposals.AddSceneAsync(scene, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProposalSceneDto>.Created(
            scene.Adapt<ProposalSceneDto>(),
            "Proposal scene created successfully.");
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
                "PROPOSAL_NOT_FOUND",
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
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.NotFound(ProposalNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanStaffAccessProposal(proposal, currentUserId, roleName))
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Forbidden("You do not have access to sync proposal items.");
        }

        if (proposal.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.BadRequest(
                "INVALID_PROPOSAL_STATUS",
                "Proposal items can only be synced for draft proposal."));
        }

        var scene = await _proposals.GetSceneContextAsync(proposalId, request.SceneId, cancellationToken);
        if (scene is null)
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.BadRequest(
                "INVALID_SCENE",
                "Scene does not belong to this proposal."));
        }

        var productVersions = await GetProductVersionsForSyncAsync(
            request.Items,
            scene.ProjectId,
            cancellationToken);
        if (productVersions.Count != request.Items.Select(item => item.ProductVersionId).Distinct().Count())
        {
            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Failure(Error.NotFound(
                "PRODUCT_VERSION_NOT_FOUND",
                "One or more product versions do not exist."));
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var existingItems = await _proposals.GetItemsBySceneAsync(proposalId, request.SceneId, cancellationToken);
            var syncedItems = await UpsertProposalItemsAsync(
                request,
                scene,
                productVersions,
                existingItems,
                now,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ServiceResult<SyncProposalItemsFromSceneResponseDto>.Success(
                new SyncProposalItemsFromSceneResponseDto
                {
                    ProposalId = proposalId,
                    SceneId = request.SceneId,
                    Items = syncedItems
                },
                "Proposal items synced from scene successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
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
                "PROPOSAL_NOT_FOUND",
                ProposalNotFoundMessage));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || proposal.CustomerId != currentUserId)
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Forbidden("You do not have access to select this proposal.");
        }

        if (!IsSelectableFinalProposal(proposal.Status))
        {
            return ServiceResult<SelectFinalProposalResponseDto>.Failure(Error.BadRequest(
                "INVALID_PROPOSAL_STATUS",
                "Only published proposals can be selected as final."));
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

    private static List<string> ValidateCreateSceneRequest(CreateProposalSceneRequestDto request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.SceneName))
        {
            errors.Add("Scene name is required.");
        }
        else if (request.SceneName.Trim().Length > MaxSceneNameLength)
        {
            errors.Add("Scene name must not exceed 150 characters.");
        }

        if (request.SceneType is null)
        {
            errors.Add("Scene type is required.");
        }

        return errors;
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

        if (request.Items.Count == 0)
        {
            errors.Add("At least one proposal item is required.");
            return errors;
        }

        foreach (var item in request.Items)
        {
            ValidateSyncItem(item, errors);
        }

        return errors;
    }

    private static void ValidateSyncItem(SyncProposalItemFromSceneDto item, ICollection<string> errors)
    {
        if (item.ProductVersionId == Guid.Empty)
        {
            errors.Add("Product version id is required.");
        }

        if (item.Quantity < 1)
        {
            errors.Add("Quantity must be greater than zero.");
        }

        if (NormalizeOptional(item.CustomizationNote)?.Length > MaxCustomizationNoteLength)
        {
            errors.Add("Customization note must not exceed 1000 characters.");
        }
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
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsSales(roleName))
        {
            return proposal.AssignedSalesId == currentUserId;
        }

        return IsDesigner(roleName) && proposal.AssignedDesignerId == currentUserId;
    }

    private async Task<Dictionary<Guid, Infrastructure.DTOs.Products.ProductVersionDetailReadModel>> GetProductVersionsForSyncAsync(
        IEnumerable<SyncProposalItemFromSceneDto> items,
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

    private async Task<List<SyncedProposalItemDto>> UpsertProposalItemsAsync(
        SyncProposalItemsFromSceneRequestDto request,
        ProposalSceneContextReadModel scene,
        IReadOnlyDictionary<Guid, Infrastructure.DTOs.Products.ProductVersionDetailReadModel> productVersions,
        IReadOnlyList<ProposalItem> existingItems,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var syncedItems = new List<SyncedProposalItemDto>();
        foreach (var requestItem in request.Items)
        {
            var productVersion = productVersions[requestItem.ProductVersionId];
            var proposalItem = FindExistingProposalItem(existingItems, requestItem.ProductVersionId)
                ?? await CreateProposalItemAsync(scene, requestItem.ProductVersionId, now, cancellationToken);
            ApplyProposalItemSnapshot(proposalItem, productVersion, requestItem, now);
            syncedItems.Add(ToSyncedItemDto(proposalItem, requestItem.SceneObjectId, productVersion));
        }

        return syncedItems;
    }

    private async Task<ProposalItem> CreateProposalItemAsync(
        ProposalSceneContextReadModel scene,
        Guid productVersionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var proposalItem = new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = scene.ProposalId,
            SceneId = scene.SceneId,
            ProjectAreaId = scene.ProjectAreaId,
            ProductVersionId = productVersionId,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _proposals.AddItemAsync(proposalItem, cancellationToken);
        return proposalItem;
    }

    private static ProposalItem? FindExistingProposalItem(
        IEnumerable<ProposalItem> existingItems,
        Guid productVersionId)
    {
        return existingItems.FirstOrDefault(item => item.ProductVersionId == productVersionId);
    }

    private static void ApplyProposalItemSnapshot(
        ProposalItem proposalItem,
        Infrastructure.DTOs.Products.ProductVersionDetailReadModel productVersion,
        SyncProposalItemFromSceneDto requestItem,
        DateTime now)
    {
        var quantity = requestItem.Quantity;
        var unitPrice = productVersion.EstimatedPrice ?? 0m;
        proposalItem.ItemName = productVersion.ProductName ?? productVersion.VersionName;
        proposalItem.ItemType = productVersion.VersionType?.ToString();
        proposalItem.Width = productVersion.Width;
        proposalItem.Height = productVersion.Height;
        proposalItem.Depth = productVersion.Depth;
        proposalItem.Material = productVersion.Material;
        proposalItem.Color = productVersion.Color;
        proposalItem.Quantity = quantity;
        proposalItem.UnitPriceSnapshot = unitPrice;
        proposalItem.TotalPriceSnapshot = unitPrice * quantity;
        proposalItem.Note = NormalizeOptional(requestItem.CustomizationNote);
        proposalItem.UpdatedAt = now;
    }

    private static SyncedProposalItemDto ToSyncedItemDto(
        ProposalItem proposalItem,
        string? sceneObjectId,
        Infrastructure.DTOs.Products.ProductVersionDetailReadModel productVersion)
    {
        return new SyncedProposalItemDto
        {
            ProposalItemId = proposalItem.ProposalItemId,
            SceneObjectId = NormalizeOptional(sceneObjectId),
            ProductVersionId = proposalItem.ProductVersionId,
            ProductNameSnapshot = proposalItem.ItemName,
            VersionNameSnapshot = productVersion.VersionName,
            Quantity = proposalItem.Quantity,
            UnitPriceSnapshot = proposalItem.UnitPriceSnapshot,
            SubtotalAmount = proposalItem.TotalPriceSnapshot,
            CustomizationNote = proposalItem.Note
        };
    }

    private static bool IsSelectableFinalProposal(ProposalStatus? status)
    {
        return status is ProposalStatus.PUBLISHED or ProposalStatus.VIEWED;
    }

    private async Task DispatchProposalFinalSelectedNotificationAsync(
        ProposalDetailReadModel proposal,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var receiverIds = new[] { proposal.AssignedSalesId, proposal.AssignedDesignerId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
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
        return string.Equals(roleName, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, CustomerRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSales(string? roleName)
    {
        return string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
