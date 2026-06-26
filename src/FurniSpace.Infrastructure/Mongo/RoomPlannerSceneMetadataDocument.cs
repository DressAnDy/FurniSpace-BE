using MongoDB.Bson.Serialization.Attributes;

namespace FurniSpace.Infrastructure.Mongo;

public sealed class RoomPlannerSceneMetadataDocument
{
    [BsonElement("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [BsonElement("createdBy")]
    public Guid? CreatedBy { get; set; }

    [BsonElement("updatedBy")]
    public Guid? UpdatedBy { get; set; }
}
