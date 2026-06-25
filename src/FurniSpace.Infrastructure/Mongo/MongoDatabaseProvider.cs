using FurniSpace.Infrastructure.Common.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace FurniSpace.Infrastructure.Mongo;

public interface IMongoDatabaseProvider
{
    IMongoDatabase GetDatabase();
}

public sealed class MongoDatabaseProvider : IMongoDatabaseProvider
{
    private static readonly object ConventionLock = new();
    private static bool s_conventionRegistered;
    private readonly MongoDbSettings _settings;
    private readonly Lazy<IMongoDatabase> _database;

    public MongoDatabaseProvider(IOptions<MongoDbSettings> settings)
    {
        RegisterConventions();
        _settings = settings.Value;
        _database = new Lazy<IMongoDatabase>(CreateDatabase);
    }

    public IMongoDatabase GetDatabase() => _database.Value;

    private static void RegisterConventions()
    {
        if (s_conventionRegistered)
        {
            return;
        }

        lock (ConventionLock)
        {
            if (s_conventionRegistered)
            {
                return;
            }

            ConventionRegistry.Register(
                "FurniSpaceMongoConventions",
                new ConventionPack { new CamelCaseElementNameConvention() },
                _ => true);
            s_conventionRegistered = true;
        }
    }

    private IMongoDatabase CreateDatabase()
    {
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString) ||
            string.IsNullOrWhiteSpace(_settings.DatabaseName))
        {
            throw new MongoRoomPlannerException(
                RoomPlannerMongoErrorCodes.ConnectionFailed,
                "MongoDB settings are missing. Set MongoDb__ConnectionString and MongoDb__DatabaseName.");
        }

        try
        {
            var client = new MongoClient(_settings.ConnectionString);
            return client.GetDatabase(_settings.DatabaseName);
        }
        catch (Exception exception) when (exception is MongoException or ArgumentException or FormatException)
        {
            throw new MongoRoomPlannerException(
                RoomPlannerMongoErrorCodes.ConnectionFailed,
                "MongoDB connection could not be initialized.",
                exception);
        }
    }
}
