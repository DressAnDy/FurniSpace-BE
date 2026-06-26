namespace FurniSpace.Infrastructure.Mongo;

public static class RoomPlannerMongoErrorCodes
{
    public const string ConnectionFailed = "MONGO_CONNECTION_FAILED";
    public const string DocumentNotFound = "MONGO_DOCUMENT_NOT_FOUND";
    public const string OperationFailed = "MONGO_OPERATION_FAILED";
    public const string DuplicateSqlSceneId = "DUPLICATE_SQL_SCENE_ID";
}
