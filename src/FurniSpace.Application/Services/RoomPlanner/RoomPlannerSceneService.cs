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

        var payloadValidationError = ValidatePayload(request, context);
        if (payloadValidationError is not null)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(payloadValidationError);
        }

        var sceneReferenceError = await ValidateSceneReferencesAsync(request, context.ProjectId, cancellationToken);
        if (sceneReferenceError is not null)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(sceneReferenceError);
        }

        var now = DateTime.UtcNow;
        var existingDocument = await GetExistingDocumentAsync(context, cancellationToken);
        var document = BuildDocument(context, request, currentUserId, now, existingDocument);

        RoomPlannerSceneDocument saved;
        try
        {
            saved = await _sceneDocuments.UpsertBySqlSceneIdAsync(document, cancellationToken);
        }
        catch
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(Error.InternalServerError(
                RoomPlannerSaveFailedCode,
                "Room Planner scene could not be saved."));
        }
        if (string.IsNullOrWhiteSpace(saved.Id))
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(Error.InternalServerError(
                RoomPlannerSaveFailedCode,
                "Room Planner scene could not be saved."));
        }

        if (string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            try
            {
                await _proposalScenes.UpdateMongoSceneIdAsync(sceneId, saved.Id, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                return ServiceResult<RoomPlannerSceneSaveResponseDto>.Failure(Error.InternalServerError(
                    RoomPlannerSqlLinkFailedCode,
                    "Room Planner scene was saved but SQL scene link failed."));
            }
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
            ToResponse(context, document),
            "Room planner scene retrieved successfully.");
    }

    private static RoomPlannerSceneDocument BuildDocument(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        RoomPlannerScenePayloadDto request,
        Guid currentUserId,
        DateTime now,
        RoomPlannerSceneDocument? existingDocument)
    {
        return new RoomPlannerSceneDocument
        {
            Id = existingDocument?.Id,
            SchemaVersion = request.SchemaVersion,
            EditorVersion = request.EditorVersion,
            SqlSceneId = context.SceneId,
            ProposalId = context.ProposalId,
            ProjectId = context.ProjectId,
            ProjectAreaId = null,
            SceneKind = "OFFICIAL",
            Unit = NormalizeUnit(request.Unit),
            SceneLinks = new RoomPlannerSceneLinksDocument
            {
                ProjectAreaIds = context.ProjectAreaIds.ToList()
            },
            BlueprintLayout = request.BlueprintLayout,
            Layout = null,
            Objects = request.Objects,
            Layers = request.Layers,
            StylePreset = request.StylePreset,
            Camera = request.Camera,
            Lighting = request.Lighting,
            Validation = request.Validation,
            EditorState = request.EditorState,
            Metadata = new RoomPlannerMetadataDocument
            {
                CreatedBy = existingDocument?.Metadata.CreatedBy ?? currentUserId,
                UpdatedBy = currentUserId,
                CreatedAt = existingDocument?.Metadata.CreatedAt ?? now,
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
            ProjectAreaIds = context.ProjectAreaIds,
            SchemaVersion = 3,
            EditorVersion = "ROOM_PLANNER_BABYLON_V1",
            Unit = "meter",
            BlueprintLayout = CreateEmptyBlueprintLayout(context),
            Objects = [],
            Layers = [],
            Camera = new RoomPlannerCameraDocument(),
            Lighting = new RoomPlannerLightingDocument(),
            Validation = new RoomPlannerValidationDocument(),
            LastSavedAt = null
        };

    private static RoomPlannerSceneResponseDto ToResponse(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        RoomPlannerSceneDocument document) =>
        new()
        {
            SceneId = context.SceneId,
            MongoSceneId = document.Id,
            ProposalId = document.ProposalId,
            ProjectId = document.ProjectId,
            ProjectAreaIds = context.ProjectAreaIds,
            SchemaVersion = document.SchemaVersion,
            EditorVersion = document.EditorVersion,
            Unit = document.Unit,
            BlueprintLayout = document.BlueprintLayout,
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
            or ProposalStatus.REVISION_REQUESTED;
    }

    private async Task<RoomPlannerSceneDocument?> GetExistingDocumentAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            return await _sceneDocuments.GetByIdAsync(context.MongoSceneId, cancellationToken);
        }

        return await _sceneDocuments.GetBySqlSceneIdAsync(context.SceneId, cancellationToken);
    }

    private static Error? ValidatePayload(
        RoomPlannerScenePayloadDto request,
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context)
    {
        if (context.SceneType != ProposalSceneType.ROOM_PLANNER)
        {
            return Error.BadRequest(RoomPlannerSceneRequiredCode, "Only Room Planner scenes can be saved through this endpoint.");
        }

        if (request.SchemaVersion != 3)
        {
            return Error.BadRequest(
                RoomPlannerSchemaVersionUnsupportedCode,
                "Room Planner scene schemaVersion 3 is required.");
        }

        if (request.BlueprintLayout is null || request.BlueprintLayout.Floors.Count == 0)
        {
            return Error.BadRequest(BlueprintLayoutRequiredCode, "Blueprint layout with at least one floor is required.");
        }

        if (!UnitsMatch(request.Unit, request.BlueprintLayout.Unit))
        {
            return Error.BadRequest(BlueprintFloorMappingMismatchCode, "Blueprint layout unit must match scene unit.");
        }

        if (context.SceneAreas.Any(area => area.ProjectId != context.ProjectId))
        {
            return Error.BadRequest(ProjectAreaProjectMismatchCode, "One or more scene areas do not belong to the project.");
        }

        return ValidateFloorMappings(request.BlueprintLayout, context)
            ?? ValidateObjectFloorReferences(request)
            ?? ValidateStableGeometryReferences(request.BlueprintLayout);
    }

    private static Error? ValidateFloorMappings(
        RoomPlannerBlueprintLayoutDocument blueprintLayout,
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context)
    {
        if (ContainsDuplicate(blueprintLayout.Floors.Select(floor => NormalizeIdentifier(floor.Id))) ||
            ContainsDuplicate(blueprintLayout.Floors.Select(floor => floor.ProjectAreaId)))
        {
            return BlueprintMappingError();
        }

        var mappedAreaIds = context.SceneAreas
            .Select(area => area.ProjectAreaId)
            .ToHashSet();
        var floorAreaIds = blueprintLayout.Floors
            .Select(floor => floor.ProjectAreaId)
            .ToHashSet();

        return mappedAreaIds.SetEquals(floorAreaIds) ? null : BlueprintMappingError();
    }

    private static Error? ValidateObjectFloorReferences(RoomPlannerScenePayloadDto request)
    {
        var floorIds = request.BlueprintLayout!.Floors
            .Select(floor => floor.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasInvalidFloorReference = request.Objects.Any(sceneObject =>
            string.IsNullOrWhiteSpace(sceneObject.FloorId) ||
            !floorIds.Contains(sceneObject.FloorId));

        return hasInvalidFloorReference
            ? Error.BadRequest(InvalidObjectFloorReferenceCode, "Scene object references a nonexistent blueprint floor.")
            : null;
    }

    private static Error? ValidateStableGeometryReferences(RoomPlannerBlueprintLayoutDocument blueprintLayout)
    {
        foreach (var floor in blueprintLayout.Floors)
        {
            var pointIds = floor.Points
                .Select(point => NormalizeIdentifier(point.PointId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var wallIds = floor.Walls
                .Select(wall => NormalizeIdentifier(wall.WallId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (floor.Walls.Any(wall => !pointIds.Contains(NormalizeIdentifier(wall.StartPointId)) ||
                                        !pointIds.Contains(NormalizeIdentifier(wall.EndPointId))) ||
                HasInvalidOpeningReference(floor.Doors, wallIds) ||
                HasInvalidOpeningReference(floor.Windows, wallIds) ||
                HasInvalidOpeningReference(floor.Openings, wallIds))
            {
                return BlueprintMappingError();
            }
        }

        return null;
    }

    private static bool HasInvalidOpeningReference(
        IEnumerable<RoomPlannerOpeningDocument> openings,
        ISet<string> wallIds) =>
        openings.Any(opening => !wallIds.Contains(NormalizeIdentifier(opening.WallId)));

    private static Error BlueprintMappingError() =>
        Error.BadRequest(BlueprintFloorMappingMismatchCode, "Blueprint floors must match SQL scene area mappings.");

    private static bool ContainsDuplicate<T>(IEnumerable<T> values) =>
        values.GroupBy(value => value).Any(group => group.Count() > 1);

    private async Task<Error?> ValidateSceneReferencesAsync(
        RoomPlannerScenePayloadDto request,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (_productVersions is null)
        {
            return await ValidateModelFileLinksAsync(request, cancellationToken);
        }

        var productVersionIds = request.Objects
            .Select(sceneObject => sceneObject.ProductVersionId)
            .Distinct()
            .ToList();

        if (productVersionIds.Any(productVersionId => productVersionId == Guid.Empty))
        {
            return Error.BadRequest(ProductVersionNotFoundCode, "Scene object product version id is required.");
        }

        var validProductVersions = await _productVersions.GetValidDetailsAsync(
            productVersionIds,
            projectId,
            cancellationToken);

        if (validProductVersions.Count != productVersionIds.Count)
        {
            return Error.BadRequest(ProductVersionNotFoundCode, "One or more scene object product versions are invalid.");
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
            var modelFileId = sceneObject.ModelSnapshot!.ModelFileId!.Value;
            var metadata = await _projectFiles.GetFileMetadataAsync(modelFileId, cancellationToken);
            if (metadata is null)
            {
                return Error.BadRequest(ModelFileNotFoundCode, "Scene object model file does not exist.");
            }

            var fileLinks = await _projectFiles.GetFileLinkEntitiesByFileIdAsync(
                modelFileId,
                cancellationToken);
            var hasValidModelLink = fileLinks.Any(link =>
                string.Equals(link.ReferenceType, ProductVersionReferenceType, StringComparison.OrdinalIgnoreCase) &&
                link.ReferenceId == sceneObject.ProductVersionId &&
                link.FileType == FileType.MODEL_3D);

            if (!hasValidModelLink)
            {
                return Error.BadRequest(ModelFileLinkedCode, "Scene object model file is not linked to its product version.");
            }
        }

        return null;
    }

    private static bool IsRole(string role, string expectedRole) =>
        string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? "meter" : unit.Trim();

    private static string NormalizeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool UnitsMatch(string? requestUnit, string? blueprintUnit) =>
        string.Equals(NormalizeUnit(requestUnit), NormalizeUnit(blueprintUnit), StringComparison.OrdinalIgnoreCase);

    private static RoomPlannerBlueprintLayoutDocument CreateEmptyBlueprintLayout(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context) =>
        new()
        {
            Id = $"blueprint-{context.SceneId:N}",
            Name = "Room Planner Blueprint",
            Unit = "meter",
            Floors = context.SceneAreas
                .Select((area, index) => new RoomPlannerBlueprintFloorDocument
                {
                    Id = $"floor-{context.SceneId:N}-{area.ProjectAreaId:N}",
                    ProjectAreaId = area.ProjectAreaId,
                    Name = area.AreaName,
                    LevelIndex = index,
                    Elevation = 0,
                    FloorHeight = 3,
                    SlabThickness = 0.12m
                })
                .ToList()
        };
}
