namespace FurniSpace.Infrastructure.Mongo;

public sealed class MongoRoomPlannerException : Exception
{
    public MongoRoomPlannerException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
