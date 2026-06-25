using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using RoomPlannerSqlSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerProposalSceneRepository;

namespace FurniSpace.Application.Services.RoomPlanner;

public sealed class RoomPlannerSceneService : IRoomPlannerSceneService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const string SceneNotFoundMessage = "Proposal scene not found.";
    private readonly RoomPlannerSqlSceneRepository _proposalScenes;
    private readonly IRoomPlannerSceneRepository _sceneDocuments;
    private readonly IUnitOfWork _unitOfWork;

    public RoomPlannerSceneService(
        RoomPlannerSqlSceneRepository proposalScenes,
        IRoomPlannerSceneRepository sceneDocuments,
        IUnitOfWork unitOfWork)
    {
        _proposalScenes = proposalScenes;
        _sceneDocuments = sceneDocuments;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<RoomPlannerSceneSaveResponseDto>> SaveSceneAsync(
        Guid sceneId,
        SaveRoomPlannerSceneRequestDto request,
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

        if (context.ProposalStatus != ProposalStatus.DRAFT)
        {
            return ServiceResult<RoomPlannerSceneSaveResponseDto>.BadRequest("INVALID_PROPOSAL_STATUS");
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
                "Room planner scene retrieved successfully.");
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
        Infrastructure.DTOs.RoomPlanner.RoomPlannerSceneContextReadModel context,
        SaveRoomPlannerSceneRequestDto request,
        Guid currentUserId,
        DateTime now)
    {
        return new RoomPlannerSceneDocument
        {
            SchemaVersion = request.SchemaVersion,
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
        Infrastructure.DTOs.RoomPlanner.RoomPlannerSceneContextReadModel context) =>
        new()
        {
            SceneId = context.SceneId,
            MongoSceneId = null,
            SchemaVersion = 1,
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
            SchemaVersion = document.SchemaVersion,
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
        Infrastructure.DTOs.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        if (IsRole(role, AdminRole))
        {
            return true;
        }

        if (IsRole(role, CustomerRole))
        {
            return false;
        }

        return IsAssignedStaff(context, currentUserId, role);
    }

    private static bool CanViewScene(
        Infrastructure.DTOs.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        if (IsRole(role, AdminRole) || IsAssignedStaff(context, currentUserId, role))
        {
            return true;
        }

        return IsRole(role, CustomerRole) &&
            context.CustomerId == currentUserId &&
            IsCustomerVisible(context.ProposalStatus);
    }

    private static bool IsAssignedStaff(
        Infrastructure.DTOs.RoomPlanner.RoomPlannerSceneContextReadModel context,
        Guid currentUserId,
        string role)
    {
        return IsRole(role, DesignerRole) && context.AssignedDesignerId == currentUserId ||
            IsRole(role, SalesRole) && context.AssignedSalesId == currentUserId;
    }

    private static bool IsCustomerVisible(ProposalStatus? status)
    {
        return status is ProposalStatus.PUBLISHED
            or ProposalStatus.VIEWED
            or ProposalStatus.SELECTED
            or ProposalStatus.REVISION_REQUESTED;
    }

    private static bool IsRole(string role, string expectedRole) =>
        string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? "meter" : unit.Trim();
}
