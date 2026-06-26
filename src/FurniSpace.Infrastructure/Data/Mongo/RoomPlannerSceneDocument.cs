using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FurniSpace.Infrastructure.Data.Mongo;

public sealed class RoomPlannerSceneDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("sqlSceneId")]
    public Guid SqlSceneId { get; set; }

    [BsonElement("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [BsonElement("projectId")]
    public Guid? ProjectId { get; set; }

    [BsonElement("proposalId")]
    public Guid? ProposalId { get; set; }

    [BsonElement("projectAreaId")]
    public Guid? ProjectAreaId { get; set; }

    [BsonElement("proposalItemId")]
    public Guid? ProposalItemId { get; set; }

    [BsonElement("productVersionId")]
    public Guid? ProductVersionId { get; set; }

    [BsonElement("modelFileIdSnapshot")]
    public Guid? ModelFileIdSnapshot { get; set; }

    [BsonElement("sceneKind")]
    public string SceneKind { get; set; } = "OFFICIAL";

    [BsonElement("unit")]
    public string Unit { get; set; } = "meter";

    [BsonElement("layout")]
    public RoomPlannerLayoutDocument Layout { get; set; } = new();

    [BsonElement("objects")]
    public List<RoomPlannerObjectDocument> Objects { get; set; } = [];

    [BsonElement("layers")]
    public List<RoomPlannerLayerDocument> Layers { get; set; } = [];

    [BsonElement("stylePreset")]
    public string? StylePreset { get; set; }

    [BsonElement("camera")]
    public RoomPlannerCameraDocument Camera { get; set; } = new();

    [BsonElement("lighting")]
    public RoomPlannerLightingDocument Lighting { get; set; } = new();

    [BsonElement("validation")]
    public RoomPlannerValidationDocument Validation { get; set; } = new();

    [BsonElement("editorState")]
    public RoomPlannerEditorStateDocument? EditorState { get; set; }

    [BsonElement("metadata")]
    public RoomPlannerSceneMetadataDocument Metadata { get; set; } = new();
}
