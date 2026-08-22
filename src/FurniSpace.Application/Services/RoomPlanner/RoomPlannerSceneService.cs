using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.RoomPlanner.RoomPlannerSceneServiceConstants;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.RoomPlanner;
using FurniSpace.Shared.DTOs.Proposals;
using Mapster;
using ApplicationRoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;
using RoomPlannerSqlSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerProposalSceneRepository;

namespace FurniSpace.Application.Services.RoomPlanner;

public sealed class RoomPlannerSceneService : IRoomPlannerSceneService
{
    private const decimal StandardAreaGeometryToleranceMeters = 0.05m;

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
                await CreateEmptySceneResponseAsync(context, currentUserRole, cancellationToken),
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
            await ToResponseAsync(context, document, currentUserRole, cancellationToken),
            "Room planner scene retrieved successfully.");
    }

    public async Task<ServiceResult<ResolveRoomPlannerProductsResponseDto>> ResolveProductsAsync(
        Guid sceneId,
        ResolveRoomPlannerProductsRequestDto request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (sceneId == Guid.Empty)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.BadRequest("Scene id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request is null)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.BadRequest("Resolve products request is required.");
        }

        if (_productVersions is null || _projectFiles is null)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Failure(
                Error.InternalServerError(
                    RoomPlannerLoadFailedCode,
                    "Room planner product resolution is unavailable."));
        }

        var context = await _proposalScenes.GetContextAsync(sceneId, cancellationToken);
        if (context is null)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.NotFound(SceneNotFoundMessage);
        }

        if (!CanViewScene(context, currentUserId, currentUserRole))
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Forbidden(
                "You do not have access to resolve products for this room planner scene.");
        }

        var requestedIds = (request.ProductVersionIds ?? [])
            .Where(productVersionId => productVersionId != Guid.Empty)
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Success(
                new ResolveRoomPlannerProductsResponseDto
                {
                    SceneId = sceneId,
                    ProjectId = context.ProjectId,
                    Items = []
                },
                "Room planner products resolved successfully.");
        }

        var sceneProductVersionIds = await GetSceneProductVersionIdsAsync(context, cancellationToken);
        if (requestedIds.Any(productVersionId => !sceneProductVersionIds.Contains(productVersionId)))
        {
            return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Failure(
                Error.BadRequest(
                    RoomPlannerProductNotInSceneCode,
                    "One or more product versions are not referenced by this room planner scene."));
        }

        var validProductVersions = await _productVersions.GetValidDetailsAsync(
            requestedIds,
            context.ProjectId,
            cancellationToken);

        var customerVisibleOnly = IsCustomer(currentUserRole);
        var files = await _projectFiles.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            validProductVersions.Select(version => version.ProductVersionId).ToList(),
            customerVisibleOnly,
            cancellationToken);

        var filesByVersionId = files
            .GroupBy(file => file.ReferenceId)
            .ToDictionary(
                group => group.Key,
                group => ToCatalogFileList(group, customerVisibleOnly));

        var items = validProductVersions
            .Select(version => ToResolvedProductDto(
                version,
                filesByVersionId.GetValueOrDefault(version.ProductVersionId, [])))
            .ToList();

        return ServiceResult<ResolveRoomPlannerProductsResponseDto>.Success(
            new ResolveRoomPlannerProductsResponseDto
            {
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                Items = items
            },
            "Room planner products resolved successfully.");
    }

    private async Task<HashSet<Guid>> GetSceneProductVersionIdsAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            return [];
        }

        var document = await _sceneDocuments.GetByIdAsync(context.MongoSceneId, cancellationToken);
        if (document is null)
        {
            return [];
        }

        return document.Objects
            .Where(sceneObject =>
                string.Equals(sceneObject.ObjectType, "FURNITURE", StringComparison.OrdinalIgnoreCase) &&
                sceneObject.ProductVersionId != Guid.Empty)
            .Select(sceneObject => sceneObject.ProductVersionId)
            .ToHashSet();
    }

    private static bool IsCustomer(string role) =>
        string.Equals(role, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);

    private static RoomPlannerResolvedProductDto ToResolvedProductDto(
        Infrastructure.ReadModels.Products.ProductVersionDetailReadModel version,
        IReadOnlyList<CatalogFileDto> files) =>
        new()
        {
            ProductVersionId = version.ProductVersionId,
            ProductId = version.ProductId,
            ProductName = version.ProductName,
            VersionCode = version.VersionCode,
            VersionName = version.VersionName,
            VersionType = version.VersionType,
            Material = version.Material,
            Color = version.Color,
            Width = version.Width,
            Height = version.Height,
            Depth = version.Depth,
            DimensionUnit = version.DimensionUnit,
            EstimatedPrice = version.EstimatedPrice,
            IsProjectSpecific = version.IsProjectSpecific,
            Files = files
        };

    private static List<CatalogFileDto> ToCatalogFileList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly))
            .Adapt<List<CatalogFileDto>>();
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

    private async Task<RoomPlannerSceneResponseDto> CreateEmptySceneResponseAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        return new RoomPlannerSceneResponseDto
        {
            SceneId = context.SceneId,
            MongoSceneId = null,
            ProposalId = context.ProposalId,
            ProjectId = context.ProjectId,
            ProjectAreaIds = ToOrderedProjectAreaIds(context.SceneAreas),
            Areas = ToSceneAreaDtos(context.SceneAreas),
            AreaBlueprints = await GetAreaBlueprintsAsync(context, currentUserRole, cancellationToken),
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
    }

    private async Task<RoomPlannerSceneResponseDto> ToResponseAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        RoomPlannerSceneDocument document,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        return new RoomPlannerSceneResponseDto
        {
            SceneId = context.SceneId,
            MongoSceneId = document.Id,
            ProposalId = document.ProposalId,
            ProjectId = document.ProjectId,
            ProjectAreaIds = ToOrderedProjectAreaIds(context.SceneAreas),
            Areas = ToSceneAreaDtos(context.SceneAreas),
            AreaBlueprints = await GetAreaBlueprintsAsync(context, currentUserRole, cancellationToken),
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
    }

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
            or ProposalStatus.PUBLISHED
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

    private async Task<List<RoomPlannerAreaBlueprintDto>> GetAreaBlueprintsAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (_projectFiles is null)
        {
            return [];
        }

        var files = await _projectFiles.GetCatalogFilesByReferencesAsync(
            "PROJECT_AREA",
            ToOrderedProjectAreaIds(context.SceneAreas),
            IsCustomer(currentUserRole),
            cancellationToken);

        return files
            .Where(file => file.IsPrimary == true && IsAreaBlueprintFileType(file.FileType))
            .OrderBy(file => file.DisplayOrder ?? int.MaxValue)
            .ThenBy(file => file.UploadedAt)
            .Select(file => new RoomPlannerAreaBlueprintDto
            {
                ProjectAreaId = file.ReferenceId,
                FileId = file.FileId,
                FileLinkId = file.FileLinkId,
                FileType = file.FileType,
                OriginalFileName = file.OriginalFileName,
                PublicUrl = file.FileUrl,
                MimeType = file.MimeType,
                DisplayOrder = file.DisplayOrder,
                IsPrimary = file.IsPrimary == true
            })
            .ToList();
    }

    private static bool IsAreaBlueprintFileType(FileType? fileType)
    {
        return fileType is FileType.FLOOR_PLAN
            or FileType.PDF_DRAWING
            or FileType.REFERENCE_IMAGE
            or FileType.LIDAR_SCAN
            or FileType.MEASUREMENT_REPORT;
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
            ?? ValidateStandardAreaGeometry(request.BlueprintLayout, context)
            ?? ValidateObjectIds(request.Objects)
            ?? ValidateObjectFloorReferences(request)
            ?? ValidateStableGeometryReferences(request.BlueprintLayout);
    }

    private static Error? ValidateStandardAreaGeometry(
        RoomPlannerBlueprintLayoutDocument blueprintLayout,
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context)
    {
        var standardAreas = context.SceneAreas
            .Where(area => !area.IsSpecialLayout && HasRectangularDimensions(area))
            .ToDictionary(area => area.ProjectAreaId);

        foreach (var floor in blueprintLayout.Floors)
        {
            if (!standardAreas.TryGetValue(floor.ProjectAreaId, out var area))
            {
                continue;
            }

            if (!MatchesStandardAreaRectangle(floor, area))
            {
                return Error.BadRequest(
                    InvalidBlueprintGeometryCode,
                    "Standard project area boundary must match the configured width and length.");
            }
        }

        return null;
    }

    private static bool MatchesStandardAreaRectangle(
        RoomPlannerBlueprintFloorDocument floor,
        Infrastructure.ReadModels.Proposals.ProposalSceneAreaReadModel area)
    {
        if (floor.Points.Count != 4 || floor.Walls.Count < 4)
        {
            return false;
        }

        var minX = floor.Points.Min(point => point.X);
        var maxX = floor.Points.Max(point => point.X);
        var minZ = floor.Points.Min(point => point.Z);
        var maxZ = floor.Points.Max(point => point.Z);
        var actualWidth = maxX - minX;
        var actualLength = maxZ - minZ;

        return NearlyEqual(actualWidth, area.Width!.Value, StandardAreaGeometryToleranceMeters) &&
            NearlyEqual(actualLength, area.Length!.Value, StandardAreaGeometryToleranceMeters) &&
            IsAxisAlignedRectangle(floor.Points, minX, maxX, minZ, maxZ);
    }

    private static bool IsAxisAlignedRectangle(
        IEnumerable<RoomPlannerPoint2Document> points,
        decimal minX,
        decimal maxX,
        decimal minZ,
        decimal maxZ)
    {
        var corners = new HashSet<(int X, int Z)>();
        foreach (var point in points)
        {
            var xEdge = ResolveEdge(point.X, minX, maxX);
            var zEdge = ResolveEdge(point.Z, minZ, maxZ);
            if (!xEdge.HasValue || !zEdge.HasValue)
            {
                return false;
            }

            corners.Add((xEdge.Value, zEdge.Value));
        }

        return corners.Count == 4;
    }

    private static int? ResolveEdge(decimal value, decimal min, decimal max)
    {
        if (NearlyEqual(value, min, StandardAreaGeometryToleranceMeters))
        {
            return 0;
        }

        return NearlyEqual(value, max, StandardAreaGeometryToleranceMeters) ? 1 : null;
    }

    private static bool NearlyEqual(decimal actual, decimal expected, decimal tolerance) =>
        Math.Abs(actual - expected) <= tolerance;

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

    private static bool HasRectangularDimensions(
        Infrastructure.ReadModels.Proposals.ProposalSceneAreaReadModel area) =>
        area.Width is > 0m && area.Length is > 0m;

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
            var floor = new RoomPlannerBlueprintFloorDocument
            {
                Id = $"floor-{context.SceneId:N}-{area.ProjectAreaId:N}",
                ProjectAreaId = area.ProjectAreaId,
                Name = area.AreaName,
                LevelIndex = index,
                Elevation = elevation,
                FloorHeight = area.Height ?? DefaultFloorHeight,
                SlabThickness = DefaultSlabThickness
            };

            if (!area.IsSpecialLayout && HasRectangularDimensions(area))
            {
                ApplyStandardAreaRectangle(floor, area);
            }

            floors.Add(floor);

            elevation += floor.FloorHeight.GetValueOrDefault(DefaultFloorHeight) + DefaultSlabThickness;
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

    private static void ApplyStandardAreaRectangle(
        RoomPlannerBlueprintFloorDocument floor,
        Infrastructure.ReadModels.Proposals.ProposalSceneAreaReadModel area)
    {
        var width = area.Width!.Value;
        var length = area.Length!.Value;
        floor.Points =
        [
            CreateBlueprintPoint("p1", 0m, 0m),
            CreateBlueprintPoint("p2", width, 0m),
            CreateBlueprintPoint("p3", width, length),
            CreateBlueprintPoint("p4", 0m, length)
        ];
        floor.Walls =
        [
            CreateBlueprintWall("w1", floor.Points[0], floor.Points[1], area.Height),
            CreateBlueprintWall("w2", floor.Points[1], floor.Points[2], area.Height),
            CreateBlueprintWall("w3", floor.Points[2], floor.Points[3], area.Height),
            CreateBlueprintWall("w4", floor.Points[3], floor.Points[0], area.Height)
        ];
        floor.Rooms =
        [
            new Dictionary<string, object?>
            {
                ["roomId"] = $"room-{area.ProjectAreaId:N}",
                ["projectAreaId"] = area.ProjectAreaId,
                ["areaSqm"] = area.AreaSqm ?? width * length,
                ["lockedBoundary"] = true
            }
        ];
    }

    private static RoomPlannerPoint2Document CreateBlueprintPoint(string pointId, decimal x, decimal z) =>
        new() { PointId = pointId, X = x, Z = z };

    private static RoomPlannerWallDocument CreateBlueprintWall(
        string wallId,
        RoomPlannerPoint2Document start,
        RoomPlannerPoint2Document end,
        decimal? height) =>
        new()
        {
            WallId = wallId,
            StartPointId = start.PointId,
            EndPointId = end.PointId,
            Start = start,
            End = end,
            Height = height ?? DefaultFloorHeight,
            Thickness = 0.1m,
            Locked = true,
            Visible = true
        };

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
                IsSpecialLayout = area.IsSpecialLayout,
                AreaSqm = area.AreaSqm,
                Width = area.Width,
                Length = area.Length,
                Height = area.Height,
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
