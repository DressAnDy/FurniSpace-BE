namespace FurniSpace.Infrastructure.Mongo;

public sealed class RoomPlannerLayoutDocument
{
    public string Type { get; set; } = "WALL_BOUNDARY";
    public bool IsClosed { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? DefaultWallHeight { get; set; }
    public decimal? DefaultWallThickness { get; set; }
    public List<RoomPlannerPoint2Document> Boundary { get; set; } = [];
    public List<RoomPlannerWallDocument> Walls { get; set; } = [];
    public List<RoomPlannerOpeningDocument> Openings { get; set; } = [];
    public RoomPlannerFloorDocument Floor { get; set; } = new();
}

public sealed class RoomPlannerPoint2Document
{
    public decimal X { get; set; }
    public decimal Z { get; set; }
}

public sealed class RoomPlannerWallDocument
{
    public string WallId { get; set; } = string.Empty;
    public RoomPlannerPoint2Document Start { get; set; } = new();
    public RoomPlannerPoint2Document End { get; set; } = new();
    public decimal? Height { get; set; }
    public decimal? Thickness { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public RoomPlannerStyleDocument Style { get; set; } = new();
}

public sealed class RoomPlannerOpeningDocument
{
    public string OpeningId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string WallId { get; set; } = string.Empty;
    public decimal? OffsetFromWallStart { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public string? SwingDirection { get; set; }
    public string? Orientation { get; set; }
    public decimal? SillHeight { get; set; }
}

public sealed class RoomPlannerFloorDocument
{
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public decimal? Rotation { get; set; }
    public decimal? Scale { get; set; }
}

public sealed class RoomPlannerStyleDocument
{
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public decimal? TextureRotation { get; set; }
    public decimal? TextureScale { get; set; }
}

public sealed class RoomPlannerObjectDocument
{
    public string ObjectId { get; set; } = string.Empty;
    public Guid? ProposalItemId { get; set; }
    public Guid ProductVersionId { get; set; }
    public string ObjectType { get; set; } = "FURNITURE";
    public string? Name { get; set; }
    public RoomPlannerTransformDocument Transform { get; set; } = new();
    public RoomPlannerDimensionsSnapshotDocument DimensionsSnapshot { get; set; } = new();
    public RoomPlannerVisualSnapshotDocument? VisualSnapshot { get; set; }
    public RoomPlannerModelSnapshotDocument? ModelSnapshot { get; set; }
    public Dictionary<string, object?> MaterialOverrides { get; set; } = [];
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
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
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Finish { get; set; }
}

public sealed class RoomPlannerModelSnapshotDocument
{
    public Guid? ModelFileId { get; set; }
    public string? Format { get; set; }
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
