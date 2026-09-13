namespace FurniSpace.Infrastructure.Common.Mongo;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string RoomPlannerScenesCollectionName { get; set; } = "room_planner_scenes";
}
