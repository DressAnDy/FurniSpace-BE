using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.RoomPlanner.RoomPlannerSceneServiceConstants;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using ApplicationRoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;
using RoomPlannerSqlSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerProposalSceneRepository;

namespace FurniSpace.Application.Services.RoomPlanner;

public sealed class RoomPlannerSceneService : IRoomPlannerSceneService
{
    private readonly RoomPlannerSqlSceneRepository _proposalScenes;
    private readonly ApplicationRoomPlannerSceneRepository _sceneDocuments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductVersionRepository? _productVersions;
    private readonly IProjectFileRepository? _projectFiles;

    public RoomPlannerSceneService(
        RoomPlannerSqlSceneRepository proposalScenes,
        ApplicationRoomPlannerSceneRepository sceneDocuments,
        IUnitOfWork unitOfWork,
        IProductVersionRepository? productVersions = null,
        IProjectFileRepository? projectFiles = null)
    {
        _proposalScenes = proposalScenes;
        _sceneDocuments = sceneDocuments;
        _unitOfWork = unitOfWork;
        _productVersions = productVersions;
        _projectFiles = projectFiles;
    }

    public async Task<ServiceResult<RoomPlannerSceneSaveResponseDto>> SaveSceneAsync(
        Guid sceneId,
        RoomPlannerScenePayloadDto request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (sceneId == Guid.Empty)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.BadRequest("Scene id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request is null)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.BadRequest("Room planner scene payload is required.");
        }

        var context = await _proposalScenes.GetContextAsync(sceneId, cancellationToken);
        if (context is null)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.NotFound(SceneNotFoundMessage);
        }

        if (!CanSaveScene(context, currentUserId, currentUserRole))
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Forbidden("You do not have access to save this room planner scene.");
        }

        if (!IsEditableProposal(context.ProposalStatus))
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(Error.BadRequest(
                ProposalNotEditableCode,
                ProposalNotEditableMessage));
        }

        var sceneReferenceError = await ValidateSceneReferencesAsync(request, context.ProjectId, cancellationToken);
        if (sceneReferenceError is not null)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(sceneReferenceError);
        }

        var now = DateTime.UtcNow;
        var document = BuildDocument(context, request, currentUserId, now);
        if (!string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            document.Id = context.MongoSceneId;
        }

        var saved = await _sceneDocuments.UpsertBySqlSceneIdAsync(document, cancellationToken);
        if (string.IsNullOrWhiteSpace(saved.Id))
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.InternalServerError("MONGO_OPERATION_FAILED");
        }

        if (string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            await _proposalScenes.UpdateMongoSceneIdAsync(sceneId, saved.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<RoomPlannerSceneSaveResponseDto>.Success(
            new RoomPlannerSceneSaveResponseDto
            {
                SceneId = sceneId,
                MongoSceneId = saved.Id,
                LastSavedAt = saved.Metadata.UpdatedAt ?? now
            },
            "Room planner scene saved successfully.");
    }

    public async Task<ServiceResult<RoomPlannerSceneResponseDto>> GetSceneAsync(
        Guid sceneId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (sceneId == Guid.Empty)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.BadRequest("Scene id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var context = await _proposalScenes.GetContextAsync(sceneId, cancellationToken);
        if (context is null)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.NotFound(SceneNotFoundMessage);
        }

        if (!CanViewScene(context, currentUserId, currentUserRole))
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Forbidden("You do not have access to view this room planner scene.");
        }

        if (string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Success(
                CreateEmptySceneResponse(context),
                "Empty Room Planner scene template returned successfully.");
        }

        var document = await _sceneDocuments.GetByIdAsync(context.MongoSceneId, cancellationToken);
        if (document is null)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.NotFound("Room planner scene document not found.");
        }

        return ServiceResult<RoomPlannerSceneResponseDto>.Success(
            ToResponse(context.SceneId, document),
            "Room planner scene retrieved successfully.");
    }

    private static RoomPlannerSceneDocument BuildDocument(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        RoomPlannerScenePayloadDto request,
        Guid currentUserId,
        DateTime now)
    {
        return new RoomPlannerSceneDocument
        {
            SchemaVersion = request.SchemaVersion,
            EditorVersion = request.EditorVersion,
            SqlSceneId = context.SceneId,
            ProposalId = context.ProposalId,
            ProjectId = context.ProjectId,
            ProjectAreaId = context.ProjectAreaId,
            SceneKind = "OFFICIAL",
            Unit = NormalizeUnit(request.Unit),
            Layout = request.Layout,
            Objects = request.Objects,
            Layers = request.Layers,
            StylePreset = request.StylePreset,
            Camera = request.Camera,
            Lighting = request.Lighting,
            Validation = request.Validation,
            EditorState = request.EditorState,
            Metadata = new RoomPlannerMetadataDocument
            {
                CreatedBy = currentUserId,
                UpdatedBy = currentUserId,
                CreatedAt = now,
                UpdatedAt = now
            }
        };
    }

    private static RoomPlannerSceneResponseDto CreateEmptySceneResponse(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context) =>
        new()
        {
            SceneId = context.SceneId,
            MongoSceneId = null,
            ProposalId = context.ProposalId,
            ProjectId = context.ProjectId,
            ProjectAreaId = context.ProjectAreaId,
            SchemaVersion = 2,
            EditorVersion = "ROOM_PLANNER_BABYLON_V1",
            Unit = "meter",
            Layout = new RoomPlannerLayoutDocument(),
            Objects = [],
            Layers = [],
            Camera = new RoomPlannerCameraDocument(),
            Lighting = new RoomPlannerLightingDocument(),
            Validation = new RoomPlannerValidationDocument(),
            LastSavedAt = null
        };

    private static RoomPlannerSceneResponseDto ToResponse(Guid sceneId, RoomPlannerSceneDocument document) =>
        new()
        {
            SceneId = sceneId,
            MongoSceneId = document.Id,
            ProposalId = document.ProposalId,
            ProjectId = document.ProjectId,
            ProjectAreaId = document.ProjectAreaId,
            SchemaVersion = document.SchemaVersion,
            EditorVersion = document.EditorVersion,
            Unit = document.Unit,
            Layout = document.Layout,
            Objects = document.Objects,
            Layers = document.Layers,
            StylePreset = document.StylePreset,
            Camera = document.Camera,
            Lighting = document.Lighting,
            Validation = document.Validation,
            EditorState = document.EditorState,
            LastSavedAt = document.Metadata.UpdatedAt
        };

    private static bool CanSaveScene(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        if (IsRole(role, ApplicationRoles.Admin))
        {
            return true;
        }

        return IsRole(role, ApplicationRoles.Designer) && context.AssignedDesignerId == currentUserId;
    }

    private static bool CanViewScene(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        if (IsRole(role, ApplicationRoles.Admin) || IsAssignedStaff(context, currentUserId, role))
        {
            return true;
        }

        return IsRole(role, ApplicationRoles.Customer) &&
            context.CustomerId == currentUserId &&
            IsCustomerVisible(context.ProposalStatus);
    }

    private static bool IsAssignedStaff(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        return IsRole(role, ApplicationRoles.Designer) && context.AssignedDesignerId == currentUserId ||
            IsRole(role, ApplicationRoles.Sales) && context.AssignedSalesId == currentUserId;
    }

    private static bool IsCustomerVisible(ProposalStatus? status)
    {
        return status is ProposalStatus.PUBLISHED
            or ProposalStatus.REVISION_REQUESTED
            or ProposalStatus.SELECTED
            or ProposalStatus.REJECTED;
    }

    private static bool IsEditableProposal(ProposalStatus? status)
    {
        return status is ProposalStatus.DRAFT
            or ProposalStatus.PUBLISHED
            or ProposalStatus.REVISION_REQUESTED;
    }

    private async Task<Error?> ValidateSceneReferencesAsync(
        RoomPlannerScenePayloadDto request,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (_productVersions is null)
        {
            return null;
        }

        var productVersionIds = request.Objects
            .Select(sceneObject => sceneObject.ProductVersionId)
            .Distinct()
            .ToList();

        if (productVersionIds.Any(productVersionId => productVersionId == Guid.Empty))
        {
            return Error.BadRequest(InvalidSceneDataCode, "Scene object product version id is required.");
        }

        var validProductVersions = await _productVersions.GetValidDetailsAsync(
            productVersionIds,
            projectId,
            cancellationToken);

        if (validProductVersions.Count != productVersionIds.Count)
        {
            return Error.BadRequest(InvalidSceneDataCode, "One or more scene object product versions are invalid.");
        }

        return await ValidateModelFileLinksAsync(request, cancellationToken);
    }

    private async Task<Error?> ValidateModelFileLinksAsync(
        RoomPlannerScenePayloadDto request,
        CancellationToken cancellationToken)
    {
        var objectsWithModelFiles = request.Objects
            .Where(sceneObject => sceneObject.ModelSnapshot?.ModelFileId.HasValue == true)
            .ToList();
        if (objectsWithModelFiles.Count == 0 || _projectFiles is null)
        {
            return null;
        }

        foreach (var sceneObject in objectsWithModelFiles)
        {
            var fileLinks = await _projectFiles.GetFileLinkEntitiesByFileIdAsync(
                sceneObject.ModelSnapshot!.ModelFileId!.Value,
                cancellationToken);
            var hasValidModelLink = fileLinks.Any(link =>
                string.Equals(link.ReferenceType, ProductVersionReferenceType, StringComparison.OrdinalIgnoreCase) &&
                link.ReferenceId == sceneObject.ProductVersionId &&
                link.FileType == FileType.MODEL_3D);

            if (!hasValidModelLink)
            {
                return Error.BadRequest(InvalidSceneDataCode, "Scene object model file is invalid.");
            }
        }

        return null;
    }

    private static bool IsRole(string role, string expectedRole) =>
        string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? "meter" : unit.Trim();
}
