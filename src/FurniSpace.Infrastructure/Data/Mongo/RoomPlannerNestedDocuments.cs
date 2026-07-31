using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Infrastructure.Data.Mongo;

public sealed class RoomPlannerLayoutDocument
    : RoomPlannerLayoutBase<
        RoomPlannerPoint2Document,
        RoomPlannerWallDocument,
        RoomPlannerOpeningDocument,
        RoomPlannerFloorDocument>
{
    public RoomPlannerLayoutDocument()
    {
        Floor = new RoomPlannerFloorDocument();
    }
}

public sealed class RoomPlannerPoint2Document
{
    public string? PointId { get; set; }
    public decimal X { get; set; }
    public decimal? Y { get; set; }
    public decimal Z { get; set; }
}

public sealed class RoomPlannerWallDocument
    : RoomPlannerWallBase<RoomPlannerPoint2Document, RoomPlannerStyleDocument>
{
    public RoomPlannerWallDocument()
    {
        Start = new RoomPlannerPoint2Document();
        End = new RoomPlannerPoint2Document();
        Style = new RoomPlannerStyleDocument();
    }
}

public sealed class RoomPlannerOpeningDocument : RoomPlannerOpeningBase
{
}

public sealed class RoomPlannerFloorDocument
{
    public string? MaterialId { get; set; }
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public string? TextureUrlSnapshot { get; set; }
    public decimal? Rotation { get; set; }
    public decimal? Scale { get; set; }
}

public sealed class RoomPlannerSceneLinksDocument : RoomPlannerSceneLinksBase
{
    public bool ContainsProjectArea(Guid projectAreaId) => ProjectAreaIds.Contains(projectAreaId);
}

public sealed class RoomPlannerBlueprintLayoutDocument
    : RoomPlannerBlueprintLayoutBase<RoomPlannerPoint2Document, RoomPlannerBlueprintFloorDocument>
{
    public RoomPlannerBlueprintFloorDocument? FindFloor(string floorId) =>
        Floors.FirstOrDefault(floor => string.Equals(floor.Id, floorId, StringComparison.OrdinalIgnoreCase));
}

public sealed class RoomPlannerBlueprintFloorDocument
    : RoomPlannerBlueprintFloorBase<RoomPlannerPoint2Document, RoomPlannerWallDocument, RoomPlannerOpeningDocument>
{
    public bool ContainsWall(string wallId) =>
        Walls.Any(wall => string.Equals(wall.WallId, wallId, StringComparison.OrdinalIgnoreCase));
}

public sealed class RoomPlannerStyleDocument
{
    public string? MaterialId { get; set; }
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public string? TextureUrlSnapshot { get; set; }
    public decimal? TextureRotation { get; set; }
    public decimal? TextureScale { get; set; }
}

public sealed class RoomPlannerObjectDocument
    : RoomPlannerObjectBase<
        RoomPlannerTransformDocument,
        RoomPlannerDimensionsSnapshotDocument,
        RoomPlannerVisualSnapshotDocument,
        RoomPlannerModelSnapshotDocument>
{
    public RoomPlannerObjectDocument()
    {
        Transform = new RoomPlannerTransformDocument();
        Placement = new RoomPlannerPlacementDocument();
        DimensionsSnapshot = new RoomPlannerDimensionsSnapshotDocument();
    }

    public string? FloorId { get; set; }
    public RoomPlannerPlacementDocument Placement { get; set; }
}

public sealed class RoomPlannerPlacementDocument
{
    public string Mode { get; set; } = "FLOOR";
    public decimal? HeightOffset { get; set; }
    public string? SupportObjectId { get; set; }
    public string? MountedWallId { get; set; }
}

public sealed class RoomPlannerTransformDocument
{
    public RoomPlannerVector3Document Position { get; set; } = new();
    public RoomPlannerVector3Document Rotation { get; set; } = new();
    public RoomPlannerVector3Document Scale { get; set; } = new() { X = 1, Y = 1, Z = 1 };
}

public sealed class RoomPlannerVector3Document
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }
}

public sealed class RoomPlannerDimensionsSnapshotDocument
{
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string Unit { get; set; } = "cm";
}

public sealed class RoomPlannerVisualSnapshotDocument
{
    public Guid? ThumbnailFileId { get; set; }
    public string? ThumbnailUrlSnapshot { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Finish { get; set; }
}

public sealed class RoomPlannerModelSnapshotDocument
{
    public Guid? ModelFileId { get; set; }
    public string? Format { get; set; }
    public string? ModelUrlSnapshot { get; set; }
}

public sealed class RoomPlannerCameraDocument
{
    public string Mode { get; set; } = "ORBIT";
    public RoomPlannerVector3Document Position { get; set; } = new();
    public RoomPlannerVector3Document Target { get; set; } = new();
    public decimal? Zoom { get; set; }
}

public sealed class RoomPlannerLightingDocument
{
    public string Preset { get; set; } = "DEFAULT";
    public string? Environment { get; set; } = "default";
    public decimal? AmbientIntensity { get; set; }
    public decimal? DirectionalIntensity { get; set; }
    public List<Dictionary<string, object?>> CustomLights { get; set; } = [];
}

public sealed class RoomPlannerValidationDocument
{
    public string Status { get; set; } = "NOT_VALIDATED";
    public List<RoomPlannerValidationIssueDocument> Warnings { get; set; } = [];
    public List<RoomPlannerValidationIssueDocument> Errors { get; set; } = [];
    public DateTime? LastValidatedAt { get; set; }
}

public sealed class RoomPlannerValidationIssueDocument
{
    public string Code { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string? ObjectId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class RoomPlannerEditorStateDocument
{
    public string? ActiveTool { get; set; }
    public string? SelectedObjectId { get; set; }
    public string? ViewMode { get; set; }
    public bool? GridEnabled { get; set; }
    public bool? SnapEnabled { get; set; }
    public Dictionary<string, object?> SnapSettings { get; set; } = [];
}

public sealed class RoomPlannerLayerDocument
{
    public string LayerId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}
