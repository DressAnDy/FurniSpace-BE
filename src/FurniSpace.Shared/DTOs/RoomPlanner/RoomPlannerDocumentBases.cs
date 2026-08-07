#nullable enable

namespace FurniSpace.Shared.DTOs.RoomPlanner;

public abstract class RoomPlannerLayoutBase<TPoint, TWall, TOpening, TFloor>
{
    public string Type { get; set; } = "WALL_BOUNDARY";
    public bool IsClosed { get; set; }
    public decimal? AreaSqFt { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? WallHeight { get; set; }
    public decimal? WallThickness { get; set; }
    public decimal? DefaultWallHeight { get; set; }
    public decimal? DefaultWallThickness { get; set; }
    public string? FloorMaterialId { get; set; }
    public string? WallMaterialId { get; set; }
    public List<TPoint> Points { get; set; } = [];
    public List<TPoint> Boundary { get; set; } = [];
    public List<TWall> Walls { get; set; } = [];
    public List<TOpening> Doors { get; set; } = [];
    public List<TOpening> Windows { get; set; } = [];
    public List<TOpening> Openings { get; set; } = [];
    public TFloor Floor { get; set; } = default!;
}

public abstract class RoomPlannerWallBase<TPoint, TStyle>
{
    public string WallId { get; set; } = string.Empty;
    public string? StartPointId { get; set; }
    public string? EndPointId { get; set; }
    public TPoint Start { get; set; } = default!;
    public TPoint End { get; set; } = default!;
    public decimal? Height { get; set; }
    public decimal? Thickness { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public TStyle Style { get; set; } = default!;
}

public class RoomPlannerOpeningBase
{
    public string OpeningId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string WallId { get; set; } = string.Empty;
    public decimal? Offset { get; set; }
    public decimal? OffsetFromWallStart { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? FloorOffset { get; set; }
    public string? SwingDirection { get; set; }
    public string? Orientation { get; set; }
    public decimal? SillHeight { get; set; }
    public bool? IsOpen { get; set; }
    public bool Locked { get; set; }
}

public abstract class RoomPlannerObjectBase<TTransform, TDimensions, TVisual, TModel>
{
    public string ObjectId { get; set; } = string.Empty;
    public Guid? ProposalItemId { get; set; }
    public Guid ProductVersionId { get; set; }
    public string? ProductModelId { get; set; }
    public string ObjectType { get; set; } = "FURNITURE";
    public string? Name { get; set; }
    public TTransform Transform { get; set; } = default!;
    public TDimensions DimensionsSnapshot { get; set; } = default!;
    public TVisual? VisualSnapshot { get; set; }
    public TModel? ModelSnapshot { get; set; }
    public Dictionary<string, object?> MaterialOverrides { get; set; } = [];
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}

public abstract class RoomPlannerSceneLinksBase
{
    public List<Guid> ProjectAreaIds { get; set; } = [];
}

public abstract class RoomPlannerBlueprintLayoutBase<TPoint, TFloor>
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Unit { get; set; } = "meter";
    public decimal? Scale { get; set; }
    public TPoint? Origin { get; set; }
    public decimal? NorthDirection { get; set; }
    public List<TFloor> Floors { get; set; } = [];
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

public abstract class RoomPlannerBlueprintFloorBase<TPoint, TWall>
{
    public string Id { get; set; } = string.Empty;
    public Guid ProjectAreaId { get; set; }
    public string? Name { get; set; }
    public int? LevelIndex { get; set; }
    public decimal? Elevation { get; set; }
    public decimal? FloorHeight { get; set; }
    public decimal? SlabThickness { get; set; }
    public List<TPoint> Points { get; set; } = [];
    public List<Dictionary<string, object?>> Rooms { get; set; } = [];
    public List<TWall> Walls { get; set; } = [];
    public List<RoomPlannerOpeningBase> Doors { get; set; } = [];
    public List<RoomPlannerOpeningBase> Windows { get; set; } = [];
    public List<RoomPlannerOpeningBase> Openings { get; set; } = [];
    public List<Dictionary<string, object?>> Slabs { get; set; } = [];
    public List<Dictionary<string, object?>> Stairs { get; set; } = [];
    public List<Dictionary<string, object?>> Balconies { get; set; } = [];
    public List<Dictionary<string, object?>> Yards { get; set; } = [];
    public List<Dictionary<string, object?>> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Beams { get; set; } = [];
}

public abstract class RoomPlannerScenePayloadBase<TLayout, TObject, TLayer, TCamera, TLighting, TValidation, TEditorState>
{
    public int SchemaVersion { get; set; } = 3;
    public string? EditorVersion { get; set; }
    public string Unit { get; set; } = "meter";
    public TLayout Layout { get; set; } = default!;
    public List<TObject> Objects { get; set; } = [];
    public List<TLayer> Layers { get; set; } = [];
    public string? StylePreset { get; set; }
    public TCamera Camera { get; set; } = default!;
    public TLighting Lighting { get; set; } = default!;
    public TValidation Validation { get; set; } = default!;
    public TEditorState? EditorState { get; set; }
}
