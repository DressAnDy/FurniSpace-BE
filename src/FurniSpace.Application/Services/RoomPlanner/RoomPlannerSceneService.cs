using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.RoomPlanner.RoomPlannerSceneServiceConstants;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.RoomPlanner;
using FurniSpace.Shared.DTOs.Proposals;
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

        NormalizePayload(request);

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
        RoomPlannerSceneDocument? existingDocument;
        RoomPlannerSceneDocument saved;
        try
        {
            existingDocument = await GetExistingDocumentAsync(context, cancellationToken);
            NormalizeBlueprintForNewWrite(request.BlueprintLayout!);
            var document = BuildDocument(context, request, currentUserId, now, existingDocument);
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
                LastSavedAt = saved.Metadata?.UpdatedAt ?? now
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

        RoomPlannerSceneDocument? document;
        try
        {
            document = await _sceneDocuments.GetByIdAsync(context.MongoSceneId, cancellationToken);
        }
        catch
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Failure(Error.InternalServerError(
                RoomPlannerLoadFailedCode,
                "Room Planner scene could not be loaded."));
        }

        if (document is null)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Failure(Error.NotFound(
                RoomPlannerDocumentNotFoundCode,
                "Room planner scene document not found."));
        }

        var documentValidationError = ValidateDocumentForLoad(context, document);
        if (documentValidationError is not null)
        {
            return ServiceResult<RoomPlannerSceneResponseDto>.Failure(documentValidationError);
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
                ProjectAreaIds = context.GetProjectAreaIds()
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
                CreatedBy = existingDocument?.Metadata?.CreatedBy ?? currentUserId,
                UpdatedBy = currentUserId,
                CreatedAt = existingDocument?.Metadata?.CreatedAt ?? now,
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
            ProjectAreaIds = ToOrderedProjectAreaIds(context.SceneAreas),
            Areas = ToSceneAreaDtos(context.SceneAreas),
            SchemaVersion = 3,
            EditorVersion = EmptyTemplateEditorVersion,
            Unit = "meter",
            BlueprintLayout = CreateEmptyBlueprintLayout(context),
            Objects = [],
            Layers = [],
            Camera = new RoomPlannerCameraDocument(),
            Lighting = new RoomPlannerLightingDocument(),
            Validation = new RoomPlannerValidationDocument(),
            EditorState = new RoomPlannerEditorStateDocument(),
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
            ProjectAreaIds = ToOrderedProjectAreaIds(context.SceneAreas),
            Areas = ToSceneAreaDtos(context.SceneAreas),
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
            LastSavedAt = document.Metadata?.UpdatedAt
        };

    private static Error? ValidateDocumentForLoad(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        RoomPlannerSceneDocument document)
    {
        if (document.SchemaVersion != 3 ||
            document.BlueprintLayout is null ||
            document.SqlSceneId != context.SceneId ||
            document.ProposalId != context.ProposalId ||
            document.ProjectId != context.ProjectId)
        {
            return Error.BadRequest(RoomPlannerDocumentInvalidCode, "Room planner scene document is invalid.");
        }

        return null;
    }

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

    private static void NormalizePayload(RoomPlannerScenePayloadDto request)
    {
        request.Objects ??= [];
        request.Layers ??= [];
        request.Camera ??= new RoomPlannerCameraDocument();
        request.Lighting ??= new RoomPlannerLightingDocument();
        request.Lighting.CustomLights ??= [];
        request.Validation ??= new RoomPlannerValidationDocument();

        if (request.EditorState is not null)
        {
            request.EditorState.SnapSettings ??= [];
        }

        if (request.BlueprintLayout is null)
        {
            return;
        }

        request.BlueprintLayout.Floors ??= [];
        request.BlueprintLayout.Metadata ??= [];

        foreach (var floor in request.BlueprintLayout.Floors)
        {
            floor.Points ??= [];
            floor.Walls ??= [];
            floor.Doors ??= [];
            floor.Windows ??= [];
            floor.Openings ??= [];
            floor.Rooms ??= [];
            floor.Slabs ??= [];
            floor.Stairs ??= [];
            floor.Balconies ??= [];
            floor.Yards ??= [];
            floor.Columns ??= [];
            floor.Beams ??= [];

            foreach (var wall in floor.Walls)
            {
                wall.Start ??= new RoomPlannerPoint2Document();
                wall.End ??= new RoomPlannerPoint2Document();
                wall.Style ??= new RoomPlannerStyleDocument();
            }
        }

        foreach (var sceneObject in request.Objects)
        {
            sceneObject.MaterialOverrides ??= [];
            sceneObject.Transform ??= new RoomPlannerTransformDocument();
            sceneObject.DimensionsSnapshot ??= new RoomPlannerDimensionsSnapshotDocument();
            sceneObject.Placement ??= new RoomPlannerPlacementDocument();
        }
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

        if (request.BlueprintLayout is null)
        {
            return Error.BadRequest(BlueprintLayoutRequiredCode, "Blueprint layout is required.");
        }

        if (request.BlueprintLayout.Floors.Count == 0)
        {
            return Error.BadRequest(BlueprintFloorRequiredCode, "Blueprint layout must contain at least one floor.");
        }

        if (!UnitsMatch(request.Unit, request.BlueprintLayout.Unit))
        {
            return Error.BadRequest(
                RoomPlannerUnitMismatchCode,
                "Root unit must match blueprintLayout.unit.");
        }

        if (context.SceneAreas.Any(area => area.ProjectId != context.ProjectId))
        {
            return Error.BadRequest(ProjectAreaProjectMismatchCode, "One or more scene areas do not belong to the project.");
        }

        return ValidateFloorMappings(request.BlueprintLayout, context)
            ?? ValidateObjectIds(request.Objects)
            ?? ValidateObjectFloorReferences(request)
            ?? ValidateStableGeometryReferences(request.BlueprintLayout);
    }

    private static Error? ValidateFloorMappings(
        RoomPlannerBlueprintLayoutDocument blueprintLayout,
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context)
    {
        var floorIds = blueprintLayout.Floors.Select(floor => NormalizeIdentifier(floor.Id)).ToList();
        if (floorIds.Any(string.IsNullOrWhiteSpace))
        {
            return Error.BadRequest(BlueprintFloorRequiredCode, "Blueprint floor id is required.");
        }

        if (ContainsDuplicateIdentifiers(floorIds))
        {
            return Error.BadRequest(DuplicateFloorIdCode, "Blueprint floor ids must be unique.");
        }

        if (ContainsDuplicate(blueprintLayout.Floors.Select(floor => floor.ProjectAreaId)))
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

    private static Error? ValidateObjectIds(IEnumerable<RoomPlannerObjectDocument> objects)
    {
        var objectIds = objects.Select(sceneObject => NormalizeIdentifier(sceneObject.ObjectId)).ToList();
        if (objectIds.Any(string.IsNullOrWhiteSpace))
        {
            return Error.BadRequest(InvalidBlueprintGeometryCode, "Scene object id is required.");
        }

        return ContainsDuplicateIdentifiers(objectIds)
            ? Error.BadRequest(DuplicateObjectIdCode, "Scene object ids must be unique.")
            : null;
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
            ? Error.BadRequest(
                InvalidObjectFloorReferenceCode,
                "Object references unknown floorId that does not exist in blueprintLayout.floors.")
            : null;
    }

    private static Error? ValidateStableGeometryReferences(RoomPlannerBlueprintLayoutDocument blueprintLayout)
    {
        foreach (var floor in blueprintLayout.Floors)
        {
            var pointIdValues = floor.Points
                .Select(point => NormalizeIdentifier(point.PointId))
                .ToList();
            var wallIdValues = floor.Walls
                .Select(wall => NormalizeIdentifier(wall.WallId))
                .ToList();
            var openingIdValues = floor.Doors
                .Concat(floor.Windows)
                .Concat(floor.Openings)
                .Select(opening => NormalizeIdentifier(opening.OpeningId))
                .ToList();
            var pointIds = pointIdValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var wallIds = wallIdValues.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // pointId / wallId / openingId uniqueness is scoped per floor (schema v3 multi-floor).
            if (ContainsDuplicateIdentifiers(pointIdValues) ||
                ContainsDuplicateIdentifiers(wallIdValues) ||
                ContainsDuplicateIdentifiers(openingIdValues) ||
                pointIds.Contains(string.Empty) ||
                wallIds.Contains(string.Empty) ||
                openingIdValues.Any(string.IsNullOrWhiteSpace))
            {
                return Error.BadRequest(InvalidBlueprintGeometryCode, "Blueprint stable geometry ids are invalid.");
            }

            if (floor.Walls.Any(wall => HasInvalidWallPointReference(wall, pointIds)))
            {
                return Error.BadRequest(
                    InvalidWallPointReferenceCode,
                    "Wall references a point that does not exist in the same floor.");
            }

            if (HasInvalidOpeningReference(floor.Doors, wallIds) ||
                HasInvalidOpeningReference(floor.Windows, wallIds) ||
                HasInvalidOpeningReference(floor.Openings, wallIds))
            {
                return Error.BadRequest(
                    InvalidOpeningWallReferenceCode,
                    "Opening references a wall that does not exist in the same floor.");
            }
        }

        return null;
    }

    private static bool HasInvalidWallPointReference(
        RoomPlannerWallDocument wall,
        HashSet<string> pointIds)
    {
        var startPointId = NormalizeIdentifier(wall.StartPointId);
        var endPointId = NormalizeIdentifier(wall.EndPointId);
        var hasStartPointId = startPointId.Length > 0;
        var hasEndPointId = endPointId.Length > 0;

        if (!hasStartPointId && !hasEndPointId)
        {
            // Coordinate-only walls are valid in schema v3 when Start/End are present.
            return wall.Start is null || wall.End is null;
        }

        if (!hasStartPointId || !hasEndPointId)
        {
            return true;
        }

        return !pointIds.Contains(startPointId) || !pointIds.Contains(endPointId);
    }

    private static bool HasInvalidOpeningReference(
        IEnumerable<RoomPlannerOpeningBase> openings,
        HashSet<string> wallIds) =>
        openings.Any(opening => !wallIds.Contains(NormalizeIdentifier(opening.WallId)));

    private static Error BlueprintMappingError() =>
        Error.BadRequest(BlueprintFloorMappingMismatchCode, "Blueprint floors must match SQL scene area mappings.");

    private static bool ContainsDuplicate<T>(IEnumerable<T> values) =>
        values.GroupBy(value => value).Any(group => group.Count() > 1);

    private static bool ContainsDuplicateIdentifiers(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);

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

    private static void NormalizeBlueprintForNewWrite(RoomPlannerBlueprintLayoutDocument blueprintLayout)
    {
        foreach (var floor in blueprintLayout.Floors)
        {
            NormalizeOpeningOffsets(floor.Doors);
            NormalizeOpeningOffsets(floor.Windows);
            NormalizeOpeningOffsets(floor.Openings);
        }
    }

    private static void NormalizeOpeningOffsets(IEnumerable<RoomPlannerOpeningBase> openings)
    {
        foreach (var opening in openings)
        {
            opening.Offset ??= opening.OffsetFromWallStart;
            opening.OffsetFromWallStart = null;
        }
    }

    private static RoomPlannerBlueprintLayoutDocument CreateEmptyBlueprintLayout(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context)
    {
        var floors = new List<RoomPlannerBlueprintFloorDocument>();
        var elevation = 0m;

        foreach (var (area, index) in context.SceneAreas
                     .OrderBy(area => area.SortOrder)
                     .ThenBy(area => area.ProjectAreaId)
                     .Select((area, index) => (area, index)))
        {
            floors.Add(new RoomPlannerBlueprintFloorDocument
            {
                Id = $"floor-{context.SceneId:N}-{area.ProjectAreaId:N}",
                ProjectAreaId = area.ProjectAreaId,
                Name = area.AreaName,
                LevelIndex = index,
                Elevation = elevation,
                FloorHeight = DefaultFloorHeight,
                SlabThickness = DefaultSlabThickness
            });

            elevation += DefaultFloorHeight + DefaultSlabThickness;
        }

        return new RoomPlannerBlueprintLayoutDocument
        {
            Id = $"blueprint-{context.SceneId:N}",
            Name = "Room Planner Blueprint",
            Unit = "meter",
            Scale = 1,
            Origin = new RoomPlannerPoint2Document { X = 0, Z = 0 },
            Floors = floors
        };
    }

    private static List<ProposalSceneAreaDto> ToSceneAreaDtos(
        IEnumerable<Infrastructure.ReadModels.Proposals.ProposalSceneAreaReadModel> areas) =>
        areas
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.ProjectAreaId)
            .Select(area => new ProposalSceneAreaDto
            {
                ProjectAreaId = area.ProjectAreaId,
                AreaName = area.AreaName,
                AreaType = area.AreaType?.ToString(),
                FloorNumber = area.FloorNumber,
                SortOrder = area.SortOrder,
                Status = area.Status?.ToString()
            })
            .ToList();

    private static List<Guid> ToOrderedProjectAreaIds(
        IEnumerable<Infrastructure.ReadModels.Proposals.ProposalSceneAreaReadModel> areas) =>
        areas
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.ProjectAreaId)
            .Select(area => area.ProjectAreaId)
            .ToList();
}
