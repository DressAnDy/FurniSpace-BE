#nullable enable

namespace FurniSpace.Shared.DTOs.RoomPlanner;

public abstract class RoomPlannerLayoutBase<TPoint, TWall, TOpening, TFloor>
{
    public string Type { get; set; } = "WALL_BOUNDARY";
    public bool IsClosed { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? DefaultWallHeight { get; set; }
    public decimal? DefaultWallThickness { get; set; }
    public List<TPoint> Boundary { get; set; } = [];
    public List<TWall> Walls { get; set; } = [];
    public List<TOpening> Openings { get; set; } = [];
    public TFloor Floor { get; set; } = default!;
}

public abstract class RoomPlannerWallBase<TPoint, TStyle>
{
    public string WallId { get; set; } = string.Empty;
    public TPoint Start { get; set; } = default!;
    public TPoint End { get; set; } = default!;
    public decimal? Height { get; set; }
    public decimal? Thickness { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public TStyle Style { get; set; } = default!;
}

public abstract class RoomPlannerOpeningBase
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

public abstract class RoomPlannerObjectBase<TTransform, TDimensions, TVisual, TModel>
{
    public string ObjectId { get; set; } = string.Empty;
    public Guid? ProposalItemId { get; set; }
    public Guid ProductVersionId { get; set; }
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

public abstract class RoomPlannerScenePayloadBase<TLayout, TObject, TLayer, TCamera, TLighting, TValidation, TEditorState>
{
    public int SchemaVersion { get; set; } = 1;
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
